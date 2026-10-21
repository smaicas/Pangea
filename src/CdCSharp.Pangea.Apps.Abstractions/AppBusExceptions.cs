namespace CdCSharp.Pangea.Apps;

/// <summary>Something on the bus did not work out. Catch the subclasses apart: they mean different things.</summary>
public class AppBusException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// No host is running, so there is no one to send to. Worth telling the user to open the host,
/// not worth retrying in a loop.
/// </summary>
public sealed class AppBusUnavailableException(string message)
    : AppBusException(message);

/// <summary>No application handles the message, and the host could not start one that does.</summary>
/// <param name="messageId">The id nobody answers.</param>
/// <param name="message">What the host said.</param>
public sealed class AppHandlerNotFoundException(string messageId, string message)
    : AppBusException(message)
{
    public string MessageId { get; } = messageId;
}

/// <summary>
/// The handler in the other application threw. <see cref="Exception.Message"/> is its message,
/// written for whoever reads it.
/// </summary>
/// <param name="messageId">The request that failed.</param>
/// <param name="message">The handler's exception message.</param>
/// <param name="remoteType">The exception type on the other side, for a log.</param>
public sealed class AppRequestFailedException(string messageId, string message, string? remoteType)
    : AppBusException(message)
{
    public string MessageId { get; } = messageId;

    public string? RemoteType { get; } = remoteType;
}
