namespace CdCSharp.Pangea.Apps;

/// <summary>
/// Talks to the other Pangea applications through the host they are all connected to.
/// </summary>
/// <remarks>
/// <para>
/// The host - Omni, or anything else running a broker - routes each message by its id. The
/// application never addresses another by name: it sends a request and whoever handles that id
/// answers, it publishes an event and whoever subscribed receives it.
/// </para>
/// <para>
/// Without a host the bus is disconnected, and stays usable: <see cref="SendAsync{TResponse}"/>
/// throws <see cref="AppBusUnavailableException"/>, <see cref="PublishAsync{TEvent}"/> reaches
/// nobody, and subscriptions wait. It connects on its own when a host appears and keeps every
/// subscription across the reconnection, so a screen can subscribe once and forget about it.
/// </para>
/// </remarks>
public interface IAppBus
{
    /// <summary>Whether a host is connected right now.</summary>
    bool IsConnected { get; }

    /// <summary>Raised on any thread when <see cref="IsConnected"/> changes.</summary>
    event EventHandler? ConnectionChanged;

    /// <summary>
    /// Sends <paramref name="request"/> to whichever application handles it and waits for the answer.
    /// </summary>
    /// <exception cref="AppBusUnavailableException">No host is connected.</exception>
    /// <exception cref="AppHandlerNotFoundException">No application handles the message, and the host could not start one that does.</exception>
    /// <exception cref="AppRequestFailedException">The handler threw.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled; the handler's token is cancelled too.
    /// </exception>
    Task<TResponse> SendAsync<TResponse>(IAppRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether some application can answer <typeparamref name="TRequest"/>: one running that
    /// handles it, or one installed that the host would start.
    /// </summary>
    /// <returns><see langword="false"/> when no host is connected.</returns>
    Task<bool> CanSendAsync<TRequest>(CancellationToken cancellationToken = default) where TRequest : IAppMessage;

    /// <summary>Tells every subscribed application about <paramref name="appEvent"/>.</summary>
    /// <remarks>Does nothing without a host: an event nobody can receive is not an error.</remarks>
    Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default) where TEvent : IAppEvent;

    /// <summary>
    /// Calls <paramref name="handler"/> with each <typeparamref name="TEvent"/> another application publishes.
    /// </summary>
    /// <remarks>
    /// The handler runs on a background thread; marshal to the UI before touching it. Dispose the
    /// result to stop - a screen should, when it goes away.
    /// </remarks>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IAppEvent;
}
