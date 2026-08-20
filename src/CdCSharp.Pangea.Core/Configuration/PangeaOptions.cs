using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace CdCSharp.Pangea.Core.Configuration;

public class PangeaOptions
{
    public static PangeaOptions Default => new()
    {
        DI = PangeaDIOptions.Default,
        Window = PangeaWindowOptions.Default,
        Startup = PangeaStartupOptions.Default
    };

    public PangeaDIOptions DI { get; set; } = PangeaDIOptions.Default;
    public PangeaWindowOptions Window { get; set; } = PangeaWindowOptions.Default;
    public PangeaStartupOptions Startup { get; set; } = PangeaStartupOptions.Default;
}

/// <summary>What to do when an <c>IPangeaAsyncInitializer</c> fails.</summary>
public enum StartupFailureBehavior
{
    /// <summary>
    /// Show the failure on the splash window and leave it there. The main window is never created:
    /// an initializer exists because the application needs what it does.
    /// </summary>
    Report,

    /// <summary>
    /// Log it and carry on to the main window. For an application whose initializers are
    /// conveniences rather than preconditions.
    /// </summary>
    Continue,

    /// <summary>Let the exception out of startup and take the process down with it.</summary>
    Throw
}

/// <summary>How the application starts when it has <c>IPangeaAsyncInitializer</c> work to do.</summary>
/// <remarks>
/// None of this is read by an application without initializers: it creates its main window and
/// shows it, and no splash is ever built.
/// </remarks>
public class PangeaStartupOptions
{
    public static PangeaStartupOptions Default => new();

    /// <summary>
    /// Whether to put a window on screen while the initializers run. Turning it off leaves the
    /// screen empty until the main window appears - which also means nothing keeps a desktop
    /// application alive if the last window closes in between.
    /// </summary>
    public bool ShowSplash { get; set; } = true;

    /// <summary>
    /// A window type to use instead of the built-in splash. Implement
    /// <c>IPangeaSplashView</c> on it to receive the running initializer's name.
    /// </summary>
    public Type? SplashWindowType { get; set; }

    /// <summary>Title of the built-in splash. Defaults to the entry assembly's name.</summary>
    public string? SplashTitle { get; set; }

    /// <summary>
    /// How long every initializer together may take before they are cancelled. Null waits forever.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    public StartupFailureBehavior FailureBehavior { get; set; } = StartupFailureBehavior.Report;
}

public class PangeaDIOptions
{
    public static PangeaDIOptions Default => new()
    {
        AutoRegisterViewModels = true,
        ViewModelLifetime = ServiceLifetime.Transient
    };

    public bool AutoRegisterViewModels { get; set; } = true;
    public List<Assembly> AdditionalAssemblies { get; } = new();
    public ServiceLifetime ViewModelLifetime { get; set; } = ServiceLifetime.Transient;
}

public class PangeaWindowOptions
{
    public static PangeaWindowOptions Default => new()
    {
        AutoDiscoverMainWindow = true
    };

    public Type? MainWindowType { get; set; }

    /// <summary>
    /// The control that is the whole application on a platform with no windows: Android, iOS, the
    /// browser. Ignored on desktop, where <see cref="MainWindowType"/> is what is shown.
    /// </summary>
    /// <remarks>
    /// Left unset, a control named <c>MainView</c> is found the same way <c>MainWindow</c> is.
    /// It must not be a <c>Window</c>: those platforms register no windowing platform, so one
    /// cannot be constructed there at all.
    /// </remarks>
    public Type? MainViewType { get; set; }

    public Type? MainViewModelType { get; set; }
    public bool AutoDiscoverMainWindow { get; set; } = true;
}