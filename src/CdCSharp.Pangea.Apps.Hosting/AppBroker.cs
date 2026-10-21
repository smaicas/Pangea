using CdCSharp.Pangea.Apps.Wire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Pipes;

namespace CdCSharp.Pangea.Apps.Hosting;

/// <summary>How a <see cref="AppBroker"/> runs.</summary>
public sealed class AppBrokerOptions
{
    /// <summary>The host's name, sent to every application on connection.</summary>
    public string HostName { get; set; } = "Pangea host";

    /// <summary>The pipe to listen on. The default is the one applications look for.</summary>
    public string PipeName { get; set; } = AppBusDefaults.DefaultPipeName;

    /// <summary>What every application is told on connection.</summary>
    public AppEnvironment Environment { get; set; } = new();

    /// <summary>
    /// Starts an application that answers the message id, for a request nobody running answers.
    /// Returns whether one is on its way; the broker then waits for it to connect.
    /// </summary>
    public Func<string, CancellationToken, Task<bool>>? LaunchHandler { get; set; }

    /// <summary>Whether <see cref="LaunchHandler"/> would find an application for the message id, without starting it.</summary>
    public Func<string, bool>? CanLaunch { get; set; }

    /// <summary>How long to wait for an application started for a request to connect.</summary>
    public TimeSpan LaunchTimeout { get; set; } = TimeSpan.FromSeconds(45);
}

/// <summary>An application connected to the broker.</summary>
/// <param name="ConnectionId">Unique per connection: the same application reconnecting gets a new one.</param>
/// <param name="Hello">What it said about itself.</param>
/// <param name="ConnectedAt">When it connected.</param>
public sealed record ConnectedApp(string ConnectionId, AppHello Hello, DateTimeOffset ConnectedAt)
{
    public string AppId => Hello.AppId;
}

/// <summary>One thing that went through the broker, for a host that shows the traffic.</summary>
public sealed record BusTrace(DateTimeOffset At, FrameKind Kind, string? Message, string? From, string? To)
{
    public string? Detail { get; init; }

    /// <summary>For an answer: how long the request took.</summary>
    public TimeSpan? Elapsed { get; init; }

    public bool Failed { get; init; }
}

/// <summary>
/// The middle of the bus: accepts applications on a named pipe and routes what they send.
/// </summary>
/// <remarks>
/// <para>
/// A request goes to the most recently connected application that handles its id - so a copy
/// started from an IDE takes over from the installed one while it runs. When none does and a
/// <see cref="AppBrokerOptions.LaunchHandler"/> is set, the broker has the host start one and
/// waits for it to connect before delivering, so the sender never needs to know whether the
/// other application was open.
/// </para>
/// <para>
/// Answers travel back under the sender's own id; the broker renumbers requests on the way out so
/// two applications using the same ids cannot cross their answers. When either end disconnects,
/// the other is told: a sender gets a fault instead of waiting forever, a handler gets cancelled.
/// </para>
/// </remarks>
public sealed class AppBroker : IAsyncDisposable
{
    private static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(10);

    private readonly AppBrokerOptions _options;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, Connection> _connections = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Inflight> _inflight = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stopping = new();
    private readonly object _gate = new();

    private TaskCompletionSource _appsChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _accepting;
    private AppEnvironment _environment;
    private long _nextId;
    private int _disposed;

    public AppBroker(AppBrokerOptions options, ILogger<AppBroker>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = (ILogger?)logger ?? NullLogger.Instance;
        _environment = options.Environment;
    }

    /// <summary>The applications connected right now, oldest first.</summary>
    public IReadOnlyList<ConnectedApp> Apps =>
        _connections.Values.Select(connection => connection.Info).OrderBy(app => app.ConnectedAt).ToArray();

    public AppEnvironment Environment => _environment;

    public string PipeName => _options.PipeName;

    /// <summary>Raised on any thread when an application connects or disconnects.</summary>
    public event EventHandler? AppsChanged;

    /// <summary>Raised on any thread for everything routed.</summary>
    public event EventHandler<BusTrace>? Traced;

    /// <summary>Starts listening. The pipe exists when this returns, so an application can connect straight away.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_accepting is not null) return;

            NamedPipeServerStream first = CreateServer();
            _accepting = Task.Run(() => AcceptAsync(first, _stopping.Token));
        }

        _logger.LogInformation("Application broker listening on {Pipe}", _options.PipeName);
    }

    /// <summary>Whether an application with this id is connected.</summary>
    public bool IsConnected(string appId) => FindApp(appId) is not null;

    /// <summary>Changes what every application is told, and tells the connected ones now.</summary>
    public async Task SetEnvironmentAsync(AppEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        _environment = environment;
        Frame frame = Frame.Of(FrameKind.Environment, environment);

        foreach (Connection connection in _connections.Values)
        {
            await TrySendAsync(connection, frame).ConfigureAwait(false);
        }

        Trace(new BusTrace(DateTimeOffset.Now, FrameKind.Environment, null, _options.HostName, "*"));
    }

    /// <summary>Asks the application to come to the front.</summary>
    /// <returns><see langword="false"/> when it is not connected.</returns>
    public async Task<bool> ActivateAsync(string appId)
    {
        if (FindApp(appId) is not { } connection) return false;

        bool sent = await TrySendAsync(connection, new Frame { Kind = FrameKind.Activate }).ConfigureAwait(false);
        Trace(new BusTrace(DateTimeOffset.Now, FrameKind.Activate, null, _options.HostName, appId) { Failed = !sent });
        return sent;
    }

    /// <summary>Asks the application to close.</summary>
    /// <returns>
    /// Whether it agreed; <see langword="null"/> when it is not connected or did not answer within
    /// <paramref name="timeout"/>, which a host reads as "stop it another way".
    /// </returns>
    public async Task<bool?> RequestCloseAsync(string appId, string reason, TimeSpan timeout)
    {
        if (FindApp(appId) is not { } connection) return null;

        string id = NextId();
        TaskCompletionSource<Frame> answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Pending[id] = answer;

        try
        {
            if (!await TrySendAsync(connection, Frame.Of(FrameKind.Close, new CloseRequest(reason), id)).ConfigureAwait(false)) return null;

            Frame reply = await answer.Task.WaitAsync(timeout).ConfigureAwait(false);
            bool agreed = reply.Kind == FrameKind.Response && reply.Read<bool>();

            Trace(new BusTrace(DateTimeOffset.Now, FrameKind.Close, null, _options.HostName, appId) { Detail = agreed ? $"{reason}: agreed" : $"{reason}: refused" });
            return agreed;
        }
        catch (TimeoutException)
        {
            return null;
        }
        finally
        {
            connection.Pending.TryRemove(id, out _);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        await _stopping.CancelAsync().ConfigureAwait(false);

        Task? accepting;
        lock (_gate) accepting = _accepting;

        if (accepting is not null)
        {
            try
            {
                await accepting.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Stopping is what was asked for.
            }
        }

        foreach (Connection connection in _connections.Values)
        {
            await connection.Frames.DisposeAsync().ConfigureAwait(false);
        }

        // The token source is left to the collector: connections still winding down read its token.
    }

    private NamedPipeServerStream CreateServer() => new(
        _options.PipeName,
        PipeDirection.InOut,
        NamedPipeServerStream.MaxAllowedServerInstances,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    private async Task AcceptAsync(NamedPipeServerStream first, CancellationToken stopping)
    {
        NamedPipeServerStream? server = first;

        while (!stopping.IsCancellationRequested)
        {
            try
            {
                server ??= CreateServer();
                await server.WaitForConnectionAsync(stopping).ConfigureAwait(false);

                NamedPipeServerStream connected = server;
                server = null;

                _ = Task.Run(() => ServeAsync(connected, stopping), CancellationToken.None);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                break;
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Accepting an application failed");

                if (server is not null) await server.DisposeAsync().ConfigureAwait(false);
                server = null;

                await Task.Delay(200, CancellationToken.None).ConfigureAwait(false);
            }
        }

        if (server is not null) await server.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken stopping)
    {
        await using FrameStream frames = new(pipe);
        Connection? connection = null;

        try
        {
            Frame? hello;

            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping))
            {
                timeout.CancelAfter(HelloTimeout);
                hello = await frames.ReadAsync(timeout.Token).ConfigureAwait(false);
            }

            if (hello?.Kind != FrameKind.Hello || hello.Read<AppHello>() is not { AppId.Length: > 0 } info) return;

            if (info.ProtocolVersion > AppBusDefaults.ProtocolVersion)
            {
                await frames.WriteAsync(new Frame
                {
                    Kind = FrameKind.Fault,
                    Code = FaultCodes.Invalid,
                    Error = $"{_options.HostName} speaks version {AppBusDefaults.ProtocolVersion} of the protocol and {info.Name} speaks {info.ProtocolVersion}. Update the host."
                }, stopping).ConfigureAwait(false);
                return;
            }

            connection = new Connection(NextId(), info, frames);

            await frames.WriteAsync(Frame.Of(FrameKind.Welcome, new HostWelcome(_options.HostName, _environment)), stopping).ConfigureAwait(false);

            _connections[connection.Id] = connection;
            _logger.LogInformation("{App} connected (pid {Pid})", info.Name, info.ProcessId);
            Trace(new BusTrace(DateTimeOffset.Now, FrameKind.Hello, null, info.AppId, _options.HostName)
            {
                Detail = string.Join(" · ", new[] { info.Version, info.Commit is { Length: > 7 } commit ? commit[..7] : info.Commit, $"pid {info.ProcessId}" }.Where(part => !string.IsNullOrEmpty(part)))
            });
            RaiseAppsChanged();

            while (await frames.ReadAsync(stopping).ConfigureAwait(false) is { } frame)
            {
                Handle(connection, frame);
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidDataException or ObjectDisposedException or System.Text.Json.JsonException)
        {
            // The application went away, or the host is stopping.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Serving {App} failed", connection?.Hello.Name ?? "an application");
        }
        finally
        {
            if (connection is not null) await DisconnectAsync(connection).ConfigureAwait(false);
        }
    }

    private void Handle(Connection connection, Frame frame)
    {
        switch (frame.Kind)
        {
            case FrameKind.Request:
                _ = Task.Run(() => RouteAsync(connection, frame));
                break;

            case FrameKind.Response or FrameKind.Fault:
                Answer(connection, frame);
                break;

            case FrameKind.Cancel:
                Cancel(connection, frame);
                break;

            case FrameKind.Event:
                _ = Task.Run(() => FanOutAsync(connection, frame));
                break;

            case FrameKind.Subscribe when frame.Message is { } subscribed:
                lock (connection.Subscriptions) connection.Subscriptions.Add(subscribed);
                break;

            case FrameKind.Unsubscribe when frame.Message is { } unsubscribed:
                lock (connection.Subscriptions) connection.Subscriptions.Remove(unsubscribed);
                break;

            case FrameKind.Query:
                bool answerable = frame.Message is { } message
                    && (FindHandler(message) is not null || (_options.CanLaunch?.Invoke(message) ?? false));
                _ = TrySendAsync(connection, Frame.Of(FrameKind.Response, answerable, frame.Id, frame.Message));
                break;
        }
    }

    private async Task RouteAsync(Connection origin, Frame request)
    {
        string message = request.Message ?? "";
        string originId = request.Id ?? NextId();
        Connection? target = FindHandler(message);

        if (target is null && _options.LaunchHandler is { } launch)
        {
            Trace(new BusTrace(DateTimeOffset.Now, FrameKind.Request, message, origin.Hello.AppId, null) { Detail = "starting an application for it" });

            try
            {
                if (await launch(message, _stopping.Token).ConfigureAwait(false))
                {
                    target = await WaitForHandlerAsync(message, _options.LaunchTimeout).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Starting an application for '{Message}' failed", message);
            }
        }

        if (target is null)
        {
            await TrySendAsync(origin, new Frame
            {
                Kind = FrameKind.Fault,
                Id = originId,
                Message = message,
                Code = FaultCodes.NotFound,
                Error = $"No application answers '{message}'."
            }).ConfigureAwait(false);

            Trace(new BusTrace(DateTimeOffset.Now, FrameKind.Fault, message, _options.HostName, origin.Hello.AppId) { Detail = "nobody answers it", Failed = true });
            return;
        }

        string id = NextId();
        _inflight[id] = new Inflight(origin, originId, target, message, DateTimeOffset.Now);

        bool sent = await TrySendAsync(target, request with { Id = id, From = origin.Hello.AppId, To = target.Hello.AppId }).ConfigureAwait(false);

        Trace(new BusTrace(DateTimeOffset.Now, FrameKind.Request, message, origin.Hello.AppId, target.Hello.AppId)
        {
            Detail = request.Payload is { } payload ? $"{payload.GetRawText().Length} bytes" : null,
            Failed = !sent
        });

        if (!sent && _inflight.TryRemove(id, out _))
        {
            await TrySendAsync(origin, Fault(originId, message, FaultCodes.Disconnected, $"{target.Hello.Name} closed before the request reached it.")).ConfigureAwait(false);
        }
    }

    private void Answer(Connection from, Frame answer)
    {
        if (answer.Id is not { } id) return;

        // An answer to something the host itself asked - a close.
        if (from.Pending.TryRemove(id, out TaskCompletionSource<Frame>? own))
        {
            own.TrySetResult(answer);
            return;
        }

        if (!_inflight.TryGetValue(id, out Inflight? inflight) || inflight.Target != from || !_inflight.TryRemove(id, out _)) return;

        _ = TrySendAsync(inflight.Origin, answer with { Id = inflight.OriginId, From = from.Hello.AppId, To = inflight.Origin.Hello.AppId });

        Trace(new BusTrace(DateTimeOffset.Now, answer.Kind, inflight.Message, from.Hello.AppId, inflight.Origin.Hello.AppId)
        {
            Elapsed = DateTimeOffset.Now - inflight.Started,
            Detail = answer.Kind == FrameKind.Fault ? answer.Error : null,
            Failed = answer.Kind == FrameKind.Fault
        });
    }

    private void Cancel(Connection origin, Frame cancel)
    {
        foreach ((string id, Inflight inflight) in _inflight)
        {
            if (inflight.Origin != origin || inflight.OriginId != cancel.Id) continue;

            _ = TrySendAsync(inflight.Target, new Frame { Kind = FrameKind.Cancel, Id = id, Message = inflight.Message });
            Trace(new BusTrace(DateTimeOffset.Now, FrameKind.Cancel, inflight.Message, origin.Hello.AppId, inflight.Target.Hello.AppId));
        }
    }

    private async Task FanOutAsync(Connection sender, Frame appEvent)
    {
        if (appEvent.Message is not { } message) return;

        Frame forwarded = appEvent with { From = sender.Hello.AppId };
        int delivered = 0;

        foreach (Connection connection in _connections.Values)
        {
            if (connection == sender) continue;

            bool subscribed;
            lock (connection.Subscriptions) subscribed = connection.Subscriptions.Contains(message);

            if (subscribed && await TrySendAsync(connection, forwarded with { To = connection.Hello.AppId }).ConfigureAwait(false)) delivered++;
        }

        Trace(new BusTrace(DateTimeOffset.Now, FrameKind.Event, message, sender.Hello.AppId, "*")
        {
            Detail = delivered == 1 ? "1 subscriber" : $"{delivered} subscribers"
        });
    }

    private async Task DisconnectAsync(Connection connection)
    {
        _connections.TryRemove(connection.Id, out _);

        foreach ((string id, Inflight inflight) in _inflight)
        {
            if (inflight.Target == connection && _inflight.TryRemove(id, out _))
            {
                await TrySendAsync(inflight.Origin, Fault(inflight.OriginId, inflight.Message, FaultCodes.Disconnected,
                    $"{connection.Hello.Name} closed before answering.")).ConfigureAwait(false);
            }
            else if (inflight.Origin == connection && _inflight.TryRemove(id, out _))
            {
                await TrySendAsync(inflight.Target, new Frame { Kind = FrameKind.Cancel, Id = id, Message = inflight.Message }).ConfigureAwait(false);
            }
        }

        foreach (TaskCompletionSource<Frame> pending in connection.Pending.Values)
        {
            pending.TrySetResult(new Frame { Kind = FrameKind.Fault, Code = FaultCodes.Disconnected });
        }

        _logger.LogInformation("{App} disconnected", connection.Hello.Name);
        Trace(new BusTrace(DateTimeOffset.Now, FrameKind.Hello, null, connection.Hello.AppId, _options.HostName) { Detail = "disconnected" });
        RaiseAppsChanged();
    }

    private async Task<Connection?> WaitForHandlerAsync(string message, TimeSpan timeout)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        deadline.CancelAfter(timeout);

        while (true)
        {
            Task changed = Volatile.Read(ref _appsChanged).Task;

            if (FindHandler(message) is { } found) return found;

            try
            {
                await changed.WaitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return FindHandler(message);
            }
        }
    }

    /// <summary>The most recently connected application answering <paramref name="message"/>.</summary>
    private Connection? FindHandler(string message) =>
        _connections.Values
            .Where(connection => connection.Hello.Handles.Contains(message, StringComparer.Ordinal))
            .MaxBy(connection => connection.Info.ConnectedAt);

    private Connection? FindApp(string appId) =>
        _connections.Values
            .Where(connection => string.Equals(connection.Hello.AppId, appId, StringComparison.OrdinalIgnoreCase))
            .MaxBy(connection => connection.Info.ConnectedAt);

    private async Task<bool> TrySendAsync(Connection connection, Frame frame)
    {
        try
        {
            await connection.Frames.WriteAsync(frame, _stopping.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            return false;
        }
    }

    private void RaiseAppsChanged()
    {
        Interlocked.Exchange(ref _appsChanged, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();

        try
        {
            AppsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A listener to the connected applications failed");
        }
    }

    private void Trace(BusTrace trace)
    {
        try
        {
            Traced?.Invoke(this, trace);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A listener to the bus traffic failed");
        }
    }

    private string NextId() => Interlocked.Increment(ref _nextId).ToString(CultureInfo.InvariantCulture);

    private static Frame Fault(string id, string message, string code, string error) => new()
    {
        Kind = FrameKind.Fault,
        Id = id,
        Message = message,
        Code = code,
        Error = error
    };

    private sealed class Connection(string id, AppHello hello, FrameStream frames)
    {
        public string Id { get; } = id;

        public AppHello Hello { get; } = hello;

        public FrameStream Frames { get; } = frames;

        public ConnectedApp Info { get; } = new(id, hello, DateTimeOffset.Now);

        public HashSet<string> Subscriptions { get; } = new(StringComparer.Ordinal);

        /// <summary>Answers the host is waiting for from this application.</summary>
        public ConcurrentDictionary<string, TaskCompletionSource<Frame>> Pending { get; } = new(StringComparer.Ordinal);
    }

    private sealed record Inflight(Connection Origin, string OriginId, Connection Target, string Message, DateTimeOffset Started);
}
