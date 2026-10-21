using CdCSharp.Pangea.Apps.Wire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Pipes;
using System.Text.Json;

namespace CdCSharp.Pangea.Apps;

/// <summary>
/// The application's end of the bus: one named pipe to the host, kept open while the host runs and
/// tried again while it does not.
/// </summary>
/// <remarks>
/// <para>
/// One reader loop per connection receives every frame and never waits on application code:
/// handlers, close requests and event subscribers each run off it, so a slow handler cannot hold
/// up the answer to a request this application sent.
/// </para>
/// <para>
/// Everything that depends on the connection is torn down with it. Requests in flight fail with
/// <see cref="AppBusUnavailableException"/> instead of waiting forever, handlers running for the
/// host are cancelled, and subscriptions are kept to be sent again on the next connection.
/// </para>
/// </remarks>
public sealed class AppBusClient : IAppBus, IAppHostContext, IAsyncDisposable, IDisposable
{
    private const int ConnectTimeoutMilliseconds = 500;
    private static readonly TimeSpan WelcomeTimeout = TimeSpan.FromSeconds(5);

    private readonly IServiceProvider _services;
    private readonly ILogger _logger;
    private readonly string _pipeName;
    private readonly TimeSpan _retryInterval;
    private readonly Dictionary<string, AppHandlerEntry> _handlers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Frame>> _pending = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _running = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Subscription>> _subscriptions = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stopping = new();
    private readonly object _gate = new();

    private Task? _loop;
    private volatile FrameStream? _connection;
    private long _nextId;
    private int _disposed;

    public AppBusClient(
        AppIdentity identity,
        IServiceProvider services,
        IOptions<AppsOptions> options,
        IEnumerable<AppHandlerEntry> handlers,
        ILogger<AppBusClient>? logger = null)
        : this(identity, services, options, handlers, logger, includeGenerated: true)
    {
    }

    /// <remarks>
    /// <c>includeGenerated</c> says whether to answer what the generated catalog lists. A test
    /// running several clients in one process gives each only the handlers it registers, as
    /// separate applications would have.
    /// </remarks>
    internal AppBusClient(
        AppIdentity identity,
        IServiceProvider services,
        IOptions<AppsOptions> options,
        IEnumerable<AppHandlerEntry> handlers,
        ILogger<AppBusClient>? logger,
        bool includeGenerated)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        Identity = identity;
        _services = services;
        _logger = (ILogger?)logger ?? NullLogger.Instance;
        _pipeName = options.Value.PipeName ?? AppBusDefaults.PipeName;
        _retryInterval = options.Value.RetryInterval;

        // Generated entries first, then any registered by hand; the first for an id wins and the
        // generator has already refused to compile two of them.
        foreach (AppHandlerEntry entry in (includeGenerated ? AppHandlerCatalog.All : []).Concat(handlers ?? []))
        {
            _handlers.TryAdd(entry.MessageId, entry);
        }
    }

    public AppIdentity Identity { get; }

    public bool LaunchedByHost { get; } = AppBusDefaults.LaunchingHost is not null;

    public bool IsConnected => _connection is not null;

    public bool IsHosted => IsConnected;

    public string? HostName { get; private set; }

    public AppEnvironment? Environment { get; private set; }

    /// <summary>The message ids this application answers.</summary>
    public IReadOnlyCollection<string> Handles => _handlers.Keys;

    public event EventHandler? ConnectionChanged;

    public event EventHandler? Changed;

    /// <summary>The host asked this application to come to the front.</summary>
    public event EventHandler? ActivateRequested;

    /// <summary>
    /// Answers the host's request to close. Without one the application agrees and does nothing,
    /// which is only right for a test.
    /// </summary>
    public Func<string, CancellationToken, Task<bool>>? CloseHandler { get; set; }

    /// <summary>Starts connecting. Safe to call more than once.</summary>
    public void Start()
    {
        lock (_gate)
        {
            _loop ??= Task.Run(() => RunAsync(_stopping.Token));
        }
    }

    public async Task<TResponse> SendAsync<TResponse>(IAppRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string message = AppMessages.IdOf(request.GetType());
        FrameStream frames = _connection ?? throw Unavailable(message);
        string id = NextId();

        Frame reply = await ExchangeAsync(frames, new Frame
        {
            Kind = FrameKind.Request,
            Id = id,
            Message = message,
            Payload = JsonSerializer.SerializeToElement(request, request.GetType(), WireJson.Options)
        }, cancellationToken).ConfigureAwait(false);

        return reply.Kind switch
        {
            FrameKind.Response => reply.Read<TResponse>()!,
            _ => throw ToException(reply, message)
        };
    }

    public async Task<bool> CanSendAsync<TRequest>(CancellationToken cancellationToken = default) where TRequest : IAppMessage
    {
        string message = AppMessages.IdOf<TRequest>();

        if (_connection is not { } frames) return false;

        try
        {
            Frame reply = await ExchangeAsync(frames, new Frame
            {
                Kind = FrameKind.Query,
                Id = NextId(),
                Message = message
            }, cancellationToken).ConfigureAwait(false);

            return reply.Kind == FrameKind.Response && reply.Read<bool>();
        }
        catch (AppBusUnavailableException)
        {
            return false;
        }
    }

    public async Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default) where TEvent : IAppEvent
    {
        ArgumentNullException.ThrowIfNull(appEvent);

        if (_connection is not { } frames) return;

        await TrySendAsync(frames, new Frame
        {
            Kind = FrameKind.Event,
            Message = AppMessages.IdOf(appEvent.GetType()),
            Payload = JsonSerializer.SerializeToElement(appEvent, appEvent.GetType(), WireJson.Options)
        }, cancellationToken).ConfigureAwait(false);
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IAppEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        string message = AppMessages.IdOf<TEvent>();
        Subscription subscription = new(typeof(TEvent), value => handler((TEvent)value));
        bool first;

        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(message, out List<Subscription>? list))
            {
                _subscriptions[message] = list = [];
            }

            first = list.Count == 0;
            list.Add(subscription);
        }

        if (first && _connection is { } frames)
        {
            _ = TrySendAsync(frames, new Frame { Kind = FrameKind.Subscribe, Message = message }, CancellationToken.None);
        }

        return new Unsubscriber(() =>
        {
            bool last = false;

            lock (_gate)
            {
                if (_subscriptions.TryGetValue(message, out List<Subscription>? list) && list.Remove(subscription) && list.Count == 0)
                {
                    _subscriptions.Remove(message);
                    last = true;
                }
            }

            if (last && _connection is { } current)
            {
                _ = TrySendAsync(current, new Frame { Kind = FrameKind.Unsubscribe, Message = message }, CancellationToken.None);
            }
        });
    }

    /// <summary>
    /// Stops without waiting for the connection to wind down: what a container disposing its
    /// singletons at exit can do. The pipe closes with the process either way.
    /// </summary>
    public void Dispose()
    {
        if (Volatile.Read(ref _disposed) == 1) return;

        try
        {
            _stopping.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Disposed asynchronously in the meantime.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        await _stopping.CancelAsync().ConfigureAwait(false);

        Task? loop;
        lock (_gate) loop = _loop;

        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Stopping is what was asked for.
            }
        }

        // The token source is left to the collector: handlers still winding down read its token.
    }

    private async Task RunAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                await ConnectAndServeAsync(stopping).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                // No host, or one that went away. The normal state of an application run on its own.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "The connection to the application host failed; trying again in {Interval}", _retryInterval);
            }

            try
            {
                await Task.Delay(_retryInterval, stopping).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ConnectAndServeAsync(CancellationToken stopping)
    {
        NamedPipeClientStream pipe = new(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        try
        {
            await pipe.ConnectAsync(ConnectTimeoutMilliseconds, stopping).ConfigureAwait(false);
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        await using FrameStream frames = new(pipe);

        await frames.WriteAsync(Frame.Of(FrameKind.Hello, BuildHello()), stopping).ConfigureAwait(false);

        Frame? welcome;

        using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping))
        {
            timeout.CancelAfter(WelcomeTimeout);
            welcome = await frames.ReadAsync(timeout.Token).ConfigureAwait(false);
        }

        if (welcome is null) return;

        if (welcome.Kind == FrameKind.Fault)
        {
            _logger.LogWarning("The application host refused this application: {Reason}", welcome.Error);
            return;
        }

        if (welcome.Kind != FrameKind.Welcome)
        {
            throw new InvalidDataException($"The host answered the greeting with '{welcome.Kind}'.");
        }

        HostWelcome? host = welcome.Read<HostWelcome>();

        HostName = host?.HostName;
        Environment = host?.Environment;
        _connection = frames;

        _logger.LogInformation("Connected to the application host {Host}", HostName);

        try
        {
            await SubscribeAllAsync(frames, stopping).ConfigureAwait(false);
            RaiseChanged(connection: true);

            while (await frames.ReadAsync(stopping).ConfigureAwait(false) is { } frame)
            {
                Dispatch(frame, frames);
            }
        }
        finally
        {
            _connection = null;
            HostName = null;
            Environment = null;

            foreach (string id in _pending.Keys)
            {
                if (_pending.TryRemove(id, out TaskCompletionSource<Frame>? answer))
                {
                    answer.TrySetException(new AppBusUnavailableException("The connection to the application host was lost before the answer arrived."));
                }
            }

            foreach (CancellationTokenSource running in _running.Values) TryCancel(running);

            _logger.LogInformation("Disconnected from the application host");
            RaiseChanged(connection: true);
        }
    }

    private void Dispatch(Frame frame, FrameStream frames)
    {
        switch (frame.Kind)
        {
            case FrameKind.Response or FrameKind.Fault:
                if (frame.Id is { } id && _pending.TryRemove(id, out TaskCompletionSource<Frame>? answer)) answer.TrySetResult(frame);
                break;

            case FrameKind.Request:
                _ = Task.Run(() => AnswerAsync(frame, frames));
                break;

            case FrameKind.Cancel:
                if (frame.Id is { } cancelled && _running.TryGetValue(cancelled, out CancellationTokenSource? running)) TryCancel(running);
                break;

            case FrameKind.Event:
                Deliver(frame);
                break;

            case FrameKind.Environment:
                Environment = frame.Read<AppEnvironment>();
                RaiseChanged(connection: false);
                break;

            case FrameKind.Activate:
                Raise(ActivateRequested);
                break;

            case FrameKind.Close:
                _ = Task.Run(() => AnswerCloseAsync(frame, frames));
                break;
        }
    }

    private async Task AnswerAsync(Frame request, FrameStream frames)
    {
        string id = request.Id ?? NextId();
        string message = request.Message ?? "";

        if (!_handlers.TryGetValue(message, out AppHandlerEntry? entry))
        {
            await TrySendAsync(frames, Fault(id, message, FaultCodes.NotFound, $"{Identity.Name} does not answer '{message}'."), CancellationToken.None)
                .ConfigureAwait(false);
            return;
        }

        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        _running[id] = cancellation;

        Frame reply;

        try
        {
            JsonElement result = await entry
                .InvokeAsync(_services, request.Payload, new AppRequestContext(request.From ?? "unknown"), cancellation.Token)
                .ConfigureAwait(false);

            reply = new Frame { Kind = FrameKind.Response, Id = id, Message = message, Payload = result };
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            reply = Fault(id, message, FaultCodes.Cancelled, "The request was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Handling '{Message}' for {Caller} failed", message, request.From);
            reply = Fault(id, message, FaultCodes.Failed, ex.Message) with { ErrorType = ex.GetType().FullName };
        }
        finally
        {
            _running.TryRemove(id, out _);
        }

        await TrySendAsync(frames, reply, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task AnswerCloseAsync(Frame request, FrameStream frames)
    {
        string reason = request.Read<CloseRequest>()?.Reason ?? CloseReasons.User;
        bool agreed;

        try
        {
            agreed = CloseHandler is null || await CloseHandler(reason, _stopping.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Deciding whether to close failed; staying open");
            agreed = false;
        }

        await TrySendAsync(frames, Frame.Of(FrameKind.Response, agreed, request.Id), CancellationToken.None).ConfigureAwait(false);
    }

    private void Deliver(Frame frame)
    {
        if (frame.Message is not { } message) return;

        Subscription[] subscribers;

        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(message, out List<Subscription>? list)) return;

            subscribers = list.ToArray();
        }

        // Read once per type rather than once per subscriber: two screens listening for the same
        // event get the same instance, and a payload that does not fit is reported once.
        Dictionary<Type, object?> read = [];

        foreach (Subscription subscriber in subscribers)
        {
            try
            {
                if (!read.TryGetValue(subscriber.Type, out object? value)) read[subscriber.Type] = value = frame.Read(subscriber.Type);

                if (value is not null) subscriber.Invoke(value);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "A subscriber to '{Message}' failed", message);
            }
        }
    }

    private async Task<Frame> ExchangeAsync(FrameStream frames, Frame request, CancellationToken cancellationToken)
    {
        string id = request.Id!;
        TaskCompletionSource<Frame> answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = answer;

        try
        {
            await frames.WriteAsync(request, cancellationToken).ConfigureAwait(false);

            using (cancellationToken.Register(() => answer.TrySetCanceled(cancellationToken)))
            {
                return await answer.Task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (request.Kind == FrameKind.Request)
            {
                _ = TrySendAsync(frames, new Frame { Kind = FrameKind.Cancel, Id = id, Message = request.Message }, CancellationToken.None);
            }

            throw;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw Unavailable(request.Message ?? request.Kind.ToString());
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task SubscribeAllAsync(FrameStream frames, CancellationToken cancellationToken)
    {
        string[] messages;

        lock (_gate) messages = _subscriptions.Keys.ToArray();

        foreach (string message in messages)
        {
            await frames.WriteAsync(new Frame { Kind = FrameKind.Subscribe, Message = message }, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task TrySendAsync(FrameStream frames, Frame frame, CancellationToken cancellationToken)
    {
        try
        {
            await frames.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // The connection went with it; the reader loop notices and cleans up.
        }
    }

    private AppHello BuildHello() => new(Identity.Id, Identity.Name)
    {
        Version = Identity.Version,
        Commit = Identity.Commit,
        ProcessId = System.Environment.ProcessId,
        ExecutablePath = System.Environment.ProcessPath,
        LaunchedByHost = LaunchedByHost,
        Handles = _handlers.Keys.Order(StringComparer.Ordinal).ToArray()
    };

    private string NextId() => Interlocked.Increment(ref _nextId).ToString(CultureInfo.InvariantCulture);

    private static Frame Fault(string id, string message, string code, string error) => new()
    {
        Kind = FrameKind.Fault,
        Id = id,
        Message = message,
        Code = code,
        Error = error
    };

    private static Exception ToException(Frame fault, string message) => fault.Code switch
    {
        FaultCodes.NotFound => new AppHandlerNotFoundException(message, fault.Error ?? $"No application answers '{message}'."),
        FaultCodes.Cancelled => new OperationCanceledException(fault.Error ?? "The request was cancelled."),
        FaultCodes.Disconnected => new AppRequestFailedException(message, fault.Error ?? "The application closed before answering.", null),
        FaultCodes.Failed => new AppRequestFailedException(message, fault.Error ?? "The request failed.", fault.ErrorType),
        _ => new AppBusException(fault.Error ?? $"'{message}' could not be delivered.")
    };

    private static AppBusUnavailableException Unavailable(string message) =>
        new($"No application host is running, so '{message}' has nowhere to go.");

    private static void TryCancel(CancellationTokenSource source)
    {
        try
        {
            source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Finished in the meantime.
        }
    }

    private void RaiseChanged(bool connection)
    {
        if (connection) Raise(ConnectionChanged);

        Raise(Changed);
    }

    private void Raise(EventHandler? handler)
    {
        try
        {
            handler?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "A listener to the application host's state failed");
        }
    }

    private sealed record Subscription(Type Type, Action<object> Invoke);

    private sealed class Unsubscriber(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;

        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}
