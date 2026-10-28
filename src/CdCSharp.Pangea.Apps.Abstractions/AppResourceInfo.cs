namespace CdCSharp.Pangea.Apps;

/// <summary>What kind of thing an application has going on.</summary>
public enum AppResourceKind
{
    /// <summary>A program it started and looks after: a server, a converter, a helper.</summary>
    Process,

    /// <summary>Something that keeps running and serving: a host, a bot, a connection to a server.</summary>
    Service,

    /// <summary>A model loaded in memory or on a GPU.</summary>
    Model,

    /// <summary>Work in progress that ends on its own: a download, an export, an answer being written.</summary>
    Task
}

/// <summary>
/// One thing an application has going on, as it tells the host: what it is, how it is doing and whether
/// the host may ask for it to be stopped.
/// </summary>
/// <param name="Id">Unique within the application while it lasts; what a stop request names.</param>
/// <param name="Kind">What kind of thing it is.</param>
/// <param name="Title">What it is, for a person: <c>GLM-4.7-Flash</c>, <c>Bot Bender</c>.</param>
public sealed record AppResourceInfo(string Id, AppResourceKind Kind, string Title)
{
    /// <summary>How it is doing, in a line: <c>CUDA0 · 9 GB</c>, <c>3 servers</c>, <c>45 %</c>.</summary>
    public string? Detail { get; init; }

    /// <summary>The process it is, or runs in, when it is one of its own.</summary>
    public int? ProcessId { get; init; }

    /// <summary>When it started.</summary>
    public DateTimeOffset Since { get; init; }

    /// <summary>Whether the application stops it when the host asks.</summary>
    public bool CanStop { get; init; }
}

/// <summary>The capabilities each end of the bus announces in its greeting: one is used only when both have it.</summary>
public static class AppBusFeatures
{
    /// <summary>
    /// The application reports what it has going on (<see cref="AppResourceInfo"/>) and stops one of them when the host
    /// asks. A host that does not announce it is never sent the list: one that does not know the frame would drop the
    /// connection.
    /// </summary>
    public const string Resources = "resources";
}
