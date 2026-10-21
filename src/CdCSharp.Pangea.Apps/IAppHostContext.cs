namespace CdCSharp.Pangea.Apps;

/// <summary>
/// Whether the application runs inside a host, and what the host decided for it.
/// </summary>
/// <remarks>
/// A screen reads this to adapt: hide a theme picker the host overrides anyway, or say that a
/// feature which needs another application only works with the host open.
/// </remarks>
public interface IAppHostContext
{
    /// <summary>This application's manifest.</summary>
    AppIdentity Identity { get; }

    /// <summary>Whether a host started this process, as opposed to an IDE or the user.</summary>
    bool LaunchedByHost { get; }

    /// <summary>Whether a host is connected right now.</summary>
    bool IsHosted { get; }

    /// <summary>The connected host's name, or null.</summary>
    string? HostName { get; }

    /// <summary>What the connected host decided, or null without one.</summary>
    AppEnvironment? Environment { get; }

    /// <summary>Raised on any thread when <see cref="IsHosted"/>, <see cref="HostName"/> or <see cref="Environment"/> change.</summary>
    event EventHandler? Changed;
}

/// <summary>
/// Lets the application take part when a host asks it to close: to say no while something is
/// running, or to tidy up before it goes.
/// </summary>
/// <remarks>
/// Register as many as needed; all are asked, in registration order, and the first that refuses
/// keeps the application open. Asked before the windows are, which still get their own say
/// through <c>Closing</c>.
/// </remarks>
public interface IAppCloseHandler
{
    /// <param name="reason">One of <see cref="Wire.CloseReasons"/>, or another a host chose.</param>
    /// <param name="cancellationToken">Cancelled when the application is stopping anyway.</param>
    /// <returns><see langword="false"/> to stay open.</returns>
    Task<bool> OnCloseRequestedAsync(string reason, CancellationToken cancellationToken);
}
