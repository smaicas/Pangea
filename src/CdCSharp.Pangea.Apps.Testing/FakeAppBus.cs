namespace CdCSharp.Pangea.Apps.Testing;

/// <summary>
/// A bus that answers requests from a script, records what was sent and published, and delivers the
/// events a test raises.
/// </summary>
/// <remarks>
/// <para>
/// It fails the way the real one does, so the paths a screen takes for each failure are testable:
/// disconnected, <see cref="SendAsync{TResponse}"/> throws <see cref="AppBusUnavailableException"/>;
/// a request nothing answers throws <see cref="AppHandlerNotFoundException"/>; an answer that throws
/// arrives as <see cref="AppRequestFailedException"/> carrying its message. Messages need their
/// <see cref="AppMessageAttribute"/>, as they do on the wire.
/// </para>
/// <code>
/// FakeAppBus bus = new FakeAppBus().Answer((MakeGreeting request) => new Greeting($"Hello, {request.Name}!"));
/// </code>
/// <para>
/// Connected from the start: most tests are about what is sent. Set <see cref="IsConnected"/> to
/// <see langword="false"/> for the screen without a host. Subscribers are called on the thread that
/// calls <see cref="Raise{TEvent}"/>, where the real bus calls them on one of its own.
/// </para>
/// </remarks>
public sealed class FakeAppBus : IAppBus
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, Func<object, CancellationToken, Task<object?>>> _answers = [];
    private readonly List<(Type Type, Action<object> Handler)> _subscribers = [];
    private readonly List<object> _sent = [];
    private readonly List<IAppEvent> _published = [];
    private bool _connected = true;

    /// <summary>Whether a host is connected. Setting it raises <see cref="ConnectionChanged"/> when it changes.</summary>
    public bool IsConnected
    {
        get => _connected;
        set
        {
            if (_connected == value) return;

            _connected = value;
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ConnectionChanged;

    /// <summary>The requests sent while connected, in order.</summary>
    public IReadOnlyList<object> Sent
    {
        get
        {
            lock (_gate) return _sent.ToArray();
        }
    }

    /// <summary>The events published while connected, in order.</summary>
    public IReadOnlyList<IAppEvent> Published
    {
        get
        {
            lock (_gate) return _published.ToArray();
        }
    }

    /// <summary>The requests of one type that were sent, in order.</summary>
    public IReadOnlyList<TRequest> SentOf<TRequest>() => Sent.OfType<TRequest>().ToArray();

    /// <summary>The last request of one type that was sent, or <see langword="null"/>.</summary>
    public TRequest? LastSent<TRequest>() where TRequest : class => Sent.OfType<TRequest>().LastOrDefault();

    /// <summary>Answers every <typeparamref name="TRequest"/> with what <paramref name="answer"/> returns, replacing any earlier answer.</summary>
    public FakeAppBus Answer<TRequest, TResponse>(Func<TRequest, TResponse> answer) where TRequest : IAppRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(answer);

        return Answer<TRequest, TResponse>((request, _) => Task.FromResult(answer(request)));
    }

    /// <summary>
    /// Answers every <typeparamref name="TRequest"/> asynchronously, with the request's cancellation:
    /// for a test about what a screen does while it waits, or when the caller gives up.
    /// </summary>
    public FakeAppBus Answer<TRequest, TResponse>(Func<TRequest, CancellationToken, Task<TResponse>> answer)
        where TRequest : IAppRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(answer);

        lock (_gate)
        {
            _answers[typeof(TRequest)] = async (request, cancellationToken) => await answer((TRequest)request, cancellationToken).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>Makes the handler of <typeparamref name="TRequest"/> fail with <paramref name="message"/>, as the other application's would.</summary>
    public FakeAppBus Fail<TRequest>(string message) where TRequest : IAppMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_gate)
        {
            _answers[typeof(TRequest)] = (_, _) => Task.FromException<object?>(new InvalidOperationException(message));
        }

        return this;
    }

    /// <summary>Stops answering <typeparamref name="TRequest"/>: no application handles it any more.</summary>
    public FakeAppBus Forget<TRequest>() where TRequest : IAppMessage
    {
        lock (_gate) _answers.Remove(typeof(TRequest));

        return this;
    }

    public async Task<TResponse> SendAsync<TResponse>(IAppRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string id = AppMessages.IdOf(request.GetType());

        if (!IsConnected) throw new AppBusUnavailableException("No host is running, so there is no one to send to.");

        cancellationToken.ThrowIfCancellationRequested();

        Func<object, CancellationToken, Task<object?>>? answer;

        lock (_gate)
        {
            _sent.Add(request);
            _answers.TryGetValue(request.GetType(), out answer);
        }

        if (answer is null) throw new AppHandlerNotFoundException(id, $"No application handles '{id}'.");

        try
        {
            return (TResponse)(await answer(request, cancellationToken).ConfigureAwait(false))!;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not AppBusException)
        {
            throw new AppRequestFailedException(id, ex.Message, ex.GetType().FullName);
        }
    }

    public Task<bool> CanSendAsync<TRequest>(CancellationToken cancellationToken = default) where TRequest : IAppMessage
    {
        lock (_gate) return Task.FromResult(IsConnected && _answers.ContainsKey(typeof(TRequest)));
    }

    public Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default) where TEvent : IAppEvent
    {
        ArgumentNullException.ThrowIfNull(appEvent);

        AppMessages.IdOf(appEvent.GetType());

        if (IsConnected)
        {
            lock (_gate) _published.Add(appEvent);
        }

        return Task.CompletedTask;
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IAppEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        (Type, Action<object>) subscriber = (typeof(TEvent), appEvent => handler((TEvent)appEvent));

        lock (_gate) _subscribers.Add(subscriber);

        return new Subscription(() =>
        {
            lock (_gate) _subscribers.Remove(subscriber);
        });
    }

    /// <summary>Delivers <paramref name="appEvent"/> to every subscriber of its type, as if another application had published it.</summary>
    /// <returns>How many subscribers received it.</returns>
    public int Raise<TEvent>(TEvent appEvent) where TEvent : IAppEvent
    {
        ArgumentNullException.ThrowIfNull(appEvent);

        Action<object>[] handlers;

        lock (_gate)
        {
            handlers = _subscribers.Where(subscriber => subscriber.Type == appEvent.GetType()).Select(subscriber => subscriber.Handler).ToArray();
        }

        foreach (Action<object> handler in handlers) handler(appEvent);

        return handlers.Length;
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) dispose();
        }
    }
}
