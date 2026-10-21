namespace CdCSharp.Pangea.Apps;

/// <summary>Something one application sends another over the bus.</summary>
/// <remarks>
/// Do not implement this directly: implement <see cref="IAppRequest{TResponse}"/> or
/// <see cref="IAppEvent"/>, and name the type with <see cref="AppMessageAttribute"/>.
/// </remarks>
public interface IAppMessage;

/// <summary>
/// A question for exactly one application, with an answer of type <typeparamref name="TResponse"/>.
/// </summary>
/// <remarks>
/// The application that answers is whichever declares a handler for the message id. When none is
/// running, a host that knows which installed application handles it may start that one first, so
/// the sender does not need to know who answers or whether it was open.
/// </remarks>
/// <example>
/// <code>
/// [AppMessage("yutuf.audio.download")]
/// public sealed record DownloadAudio(string Query) : IAppRequest&lt;DownloadAudioResult&gt;;
/// </code>
/// </example>
public interface IAppRequest<TResponse> : IAppMessage;

/// <summary>
/// Something that happened in one application, for any number of others that care.
/// </summary>
/// <remarks>
/// Fire and forget: publishing does not wait for anyone, and an event published while no host is
/// running reaches nobody. Use a request when the sender needs to know it arrived.
/// </remarks>
public interface IAppEvent : IAppMessage;
