namespace CdCSharp.Pangea.Apps.Wire;

/// <summary>What an application tells the host when it connects.</summary>
/// <param name="AppId">Its <see cref="PangeaAppAttribute.Id"/>.</param>
/// <param name="Name">Its <see cref="PangeaAppAttribute.Name"/>.</param>
public sealed record AppHello(string AppId, string Name)
{
    public int ProtocolVersion { get; init; } = AppBusDefaults.ProtocolVersion;

    public string? Version { get; init; }

    /// <summary>The source commit it was built from, when the build knew it.</summary>
    public string? Commit { get; init; }

    public int ProcessId { get; init; }

    public string? ExecutablePath { get; init; }

    /// <summary>Whether a host started it, as opposed to an IDE or the user.</summary>
    public bool LaunchedByHost { get; init; }

    /// <summary>The message ids it answers.</summary>
    public IReadOnlyList<string> Handles { get; init; } = [];
}

/// <summary>What the host answers to <see cref="AppHello"/>.</summary>
public sealed record HostWelcome(string HostName, AppEnvironment Environment);

/// <summary>Why the host asks an application to close.</summary>
/// <param name="Reason">One of <see cref="CloseReasons"/>, or anything else a host wants to say.</param>
public sealed record CloseRequest(string Reason);

/// <summary>The reasons a host asks an application to close.</summary>
public static class CloseReasons
{
    /// <summary>The person asked, from the host.</summary>
    public const string User = "user";

    /// <summary>A new version is about to replace this one.</summary>
    public const string Update = "update";

    /// <summary>Another instance is taking over, typically one under a debugger.</summary>
    public const string Replace = "replace";
}
