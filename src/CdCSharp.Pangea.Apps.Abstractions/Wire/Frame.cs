using System.Text.Json;

namespace CdCSharp.Pangea.Apps.Wire;

/// <summary>
/// One message on the pipe between an application and the host.
/// </summary>
/// <remarks>
/// <para>
/// The wire protocol is small on purpose, because both ends of it are versioned independently:
/// a length-prefixed JSON object per frame, a <see cref="Kind"/> saying what it is, an
/// <see cref="Id"/> pairing an answer with its question, and the message itself as a JSON
/// <see cref="Payload"/> named by <see cref="Message"/>.
/// </para>
/// <para>
/// Applications never see frames: <see cref="IAppBus"/> is the API. This is public for a host,
/// and for anyone writing one.
/// </para>
/// </remarks>
public sealed record Frame
{
    public FrameKind Kind { get; init; }

    /// <summary>Pairs a <see cref="FrameKind.Response"/> or <see cref="FrameKind.Fault"/> with its request.</summary>
    public string? Id { get; init; }

    /// <summary>The message id from <see cref="AppMessageAttribute"/>.</summary>
    public string? Message { get; init; }

    /// <summary>The application that sent it, filled in by the host when it forwards.</summary>
    public string? From { get; init; }

    /// <summary>The application it is delivered to, filled in by the host when it forwards.</summary>
    public string? To { get; init; }

    public JsonElement? Payload { get; init; }

    /// <summary>What went wrong, for a <see cref="FrameKind.Fault"/>: written for a person.</summary>
    public string? Error { get; init; }

    /// <summary>Which kind of failure a <see cref="FrameKind.Fault"/> is: one of <see cref="FaultCodes"/>.</summary>
    public string? Code { get; init; }

    /// <summary>The exception type behind a <see cref="FaultCodes.Failed"/>, for a log.</summary>
    public string? ErrorType { get; init; }

    public static Frame Of<T>(FrameKind kind, T payload, string? id = null, string? message = null) => new()
    {
        Kind = kind,
        Id = id,
        Message = message,
        Payload = JsonSerializer.SerializeToElement(payload, WireJson.Options)
    };

    /// <summary>The payload as <typeparamref name="T"/>, or <see langword="default"/> when there is none.</summary>
    public T? Read<T>() => Payload is { } payload ? payload.Deserialize<T>(WireJson.Options) : default;

    /// <summary>The payload as <paramref name="type"/>, or null when there is none.</summary>
    public object? Read(Type type) => Payload is { } payload ? payload.Deserialize(type, WireJson.Options) : null;
}

/// <summary>What a <see cref="Frame"/> is.</summary>
public enum FrameKind
{
    /// <summary>Application to host, first: who it is and what it handles. Payload: <see cref="AppHello"/>.</summary>
    Hello,

    /// <summary>Host to application, in reply to <see cref="Hello"/>. Payload: <see cref="HostWelcome"/>.</summary>
    Welcome,

    /// <summary>A request on its way to the application that handles it.</summary>
    Request,

    /// <summary>The answer to a <see cref="Request"/>, <see cref="Query"/> or <see cref="Close"/>.</summary>
    Response,

    /// <summary>A request that could not be answered: see <see cref="Frame.Code"/>.</summary>
    Fault,

    /// <summary>The sender gave up on a request: the handler's token is cancelled.</summary>
    Cancel,

    /// <summary>An event, to every subscriber.</summary>
    Event,

    /// <summary>Application to host: deliver events with this <see cref="Frame.Message"/> id.</summary>
    Subscribe,

    /// <summary>Application to host: stop delivering them.</summary>
    Unsubscribe,

    /// <summary>Application to host: can anyone answer <see cref="Frame.Message"/>? Answered with a boolean.</summary>
    Query,

    /// <summary>Host to application: the shared parameters changed. Payload: <see cref="AppEnvironment"/>.</summary>
    Environment,

    /// <summary>Host to application: come to the front.</summary>
    Activate,

    /// <summary>Host to application: please close. Answered with whether it agreed. Payload: <see cref="CloseRequest"/>.</summary>
    Close
}

/// <summary>The kinds of <see cref="FrameKind.Fault"/>, which the client turns into different exceptions.</summary>
public static class FaultCodes
{
    /// <summary>No application handles the message.</summary>
    public const string NotFound = "not-found";

    /// <summary>The handler threw.</summary>
    public const string Failed = "failed";

    /// <summary>The application handling it went away before answering.</summary>
    public const string Disconnected = "disconnected";

    /// <summary>The request was cancelled.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>The frame made no sense to the other side.</summary>
    public const string Invalid = "invalid";
}
