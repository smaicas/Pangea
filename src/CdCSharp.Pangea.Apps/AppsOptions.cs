namespace CdCSharp.Pangea.Apps;

/// <summary>How the application behaves as part of a host. The defaults are what an application wants.</summary>
public class AppsOptions
{
    /// <summary>
    /// Overrides the id from the manifest. Leave it alone: the manifest is what a host knows the
    /// application by, and an id that differs at runtime is an application the host cannot find.
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>Overrides the name from the manifest.</summary>
    public string? AppName { get; set; }

    /// <summary>Connect to a host when one is running, and keep trying while none is.</summary>
    public bool ConnectToHost { get; set; } = true;

    /// <summary>The pipe to connect to. Null uses <see cref="AppBusDefaults.PipeName"/>.</summary>
    public string? PipeName { get; set; }

    /// <summary>How long to wait between attempts to reach a host that is not running.</summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// One running copy per application. A second one brings the first to the front and exits -
    /// unless it runs under a debugger, in which case it asks the first to close and takes over,
    /// which is what pressing F5 while the installed copy is open means.
    /// </summary>
    /// <remarks>
    /// Two copies would write the same data folder at once, which is the reason this is on by
    /// default and not a matter of taste.
    /// </remarks>
    public bool SingleInstance { get; set; } = true;

    /// <summary>Take over from a running copy even without a debugger.</summary>
    public bool ReplaceRunningInstance { get; set; }

    /// <summary>Wear the host's theme while connected to it, and go back to the application's own after.</summary>
    public bool FollowHostTheme { get; set; } = true;

    /// <summary>Use the host's culture while connected to it, when the application is localized.</summary>
    public bool FollowHostCulture { get; set; } = true;
}
