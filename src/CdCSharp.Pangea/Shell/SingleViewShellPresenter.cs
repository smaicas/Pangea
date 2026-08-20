using Avalonia.Controls;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Shell;

/// <summary>
/// The shell of a platform that has no windows: Android, iOS, the browser.
/// </summary>
/// <remarks>
/// <para>
/// The lifetime takes one <see cref="Control"/> and shows it. That control is a
/// <see cref="PangeaShellHost"/> rather than the application's own view, so there is somewhere to
/// put the things a desktop application would have opened a window for - the splash, and every
/// modal dialog - without ever constructing one.
/// </para>
/// <para>
/// The main view is found the way the main window is: by name. <c>MainView</c> is displayed with
/// <c>MainViewModel</c>, falling back to <c>MainWindowViewModel</c> so an application can share one
/// shell view model between its desktop and mobile heads.
/// </para>
/// </remarks>
internal sealed class SingleViewShellPresenter : IShellPresenter
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISingleViewSurface _surface;
    private readonly TypeRegistry _typeRegistry;
    private readonly PangeaCatalogIndex _catalog;
    private readonly PangeaOptions _options;
    private readonly ILogger<SingleViewShellPresenter> _logger;

    private PangeaShellHost? _host;

    public SingleViewShellPresenter(
        IServiceProvider serviceProvider,
        ISingleViewSurface surface,
        IOptions<PangeaOptions> options,
        TypeRegistry typeRegistry,
        ILogger<SingleViewShellPresenter> logger,
        PangeaCatalogIndex? catalog = null)
    {
        _serviceProvider = serviceProvider;
        _surface = surface;
        _typeRegistry = typeRegistry;
        _options = options.Value;
        _logger = logger;
        _catalog = catalog ?? PangeaCatalogIndex.Empty;
    }

    public bool IsSingleView => true;

    public void ShowMain()
    {
        Control main = BuildMainView();

        Host().MainContent = main;
    }

    public Control? ShowSplash(PangeaStartupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.ShowSplash) return null;

        Control splash = CreateSplash(options);

        Host().ShowOverlay(splash);

        return splash;
    }

    public void HideSplash(Control? splash)
    {
        if (splash is null) return;

        Host().HideOverlay(splash);
    }

    public Task<bool> ShowMessageAsync(MessageDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        MessageDialogView dialog = request.CancelText is null
            ? MessageDialogView.Statement(request.Title, request.Message, request.ConfirmText)
            : MessageDialogView.Question(request.Title, request.Message, request.ConfirmText, request.CancelText);

        PangeaShellHost host = Host();
        host.ShowOverlay(dialog);

        return Answer(host, dialog);
    }

    private static async Task<bool> Answer(PangeaShellHost host, MessageDialogView dialog)
    {
        try
        {
            return await dialog.Answered;
        }
        finally
        {
            host.HideOverlay(dialog);
        }
    }

    /// <summary>
    /// The root the lifetime is showing, built on first use.
    /// </summary>
    /// <remarks>
    /// Assigned as soon as it exists rather than when the main view is ready: the splash goes into
    /// it, and on this platform the splash is the first thing there is to show.
    /// </remarks>
    private PangeaShellHost Host()
    {
        if (_host is not null) return _host;

        _host = new PangeaShellHost();
        _surface.MainView = _host;

        return _host;
    }

    private Control CreateSplash(PangeaStartupOptions options)
    {
        if (options.SplashWindowType is null) return new PangeaSplashView(options.SplashTitle);

        // A window cannot be built here at all, so an application configured for desktop gets the
        // built-in view and a warning rather than a crash on a platform it was not written for.
        if (typeof(Window).IsAssignableFrom(options.SplashWindowType))
        {
            _logger.LogWarning(
                "The configured splash '{SplashType}' is a Window, which this platform cannot open. " +
                "The built-in splash view is shown instead.",
                options.SplashWindowType.FullName);

            return new PangeaSplashView(options.SplashTitle);
        }

        if (!typeof(Control).IsAssignableFrom(options.SplashWindowType))
        {
            throw new InvalidOperationException(
                $"'{options.SplashWindowType.FullName}' is configured as the splash but does not derive from Control.");
        }

        return (Control)(_serviceProvider.GetService(options.SplashWindowType)
                         ?? Activator.CreateInstance(options.SplashWindowType)
                         ?? throw new InvalidOperationException(
                             $"The splash '{options.SplashWindowType.FullName}' could not be created."));
    }

    private Control BuildMainView()
    {
        (Type viewType, Func<object>? build) = FindMainViewType();
        Type viewModelType = FindMainViewModelType();

        Control view = (Control?)(build is not null ? build() : Activator.CreateInstance(viewType))
                       ?? throw new InvalidOperationException($"Unable to instantiate the main view '{viewType.Name}'.");

        view.DataContext = _serviceProvider.GetRequiredService(viewModelType);

        return view;
    }

    private (Type ViewType, Func<object>? Build) FindMainViewType()
    {
        if (_options.Window.MainViewType is { } configured)
        {
            return typeof(Control).IsAssignableFrom(configured) && !typeof(Window).IsAssignableFrom(configured)
                ? (configured, null)
                : throw new InvalidOperationException(
                    $"'{configured.FullName}' is configured as the main view. On a single-view platform it has to " +
                    "derive from Control and must not be a Window.");
        }

        if (_catalog.FindView("MainView") is { } generated) return (generated.ViewType, generated.Create);

        if (_typeRegistry.GetType("MainView") is { } byName) return (byName, null);

        throw new InvalidOperationException(
            "No main view was found. A single-view platform - Android, iOS, the browser - cannot open a Window, " +
            "so the application needs a control named 'MainView', or PangeaOptions.Window.MainViewType set to one.");
    }

    private Type FindMainViewModelType() =>
        _options.Window.MainViewModelType
        ?? _catalog.FindViewModel("MainViewModel")?.ViewModelType
        ?? _catalog.FindViewModel("MainWindowViewModel")?.ViewModelType
        ?? _typeRegistry.GetType("MainViewModel")
        ?? _typeRegistry.GetType("MainWindowViewModel")
        ?? throw new InvalidOperationException(
            "No main view model was found. Name one 'MainViewModel' - or 'MainWindowViewModel', which is shared " +
            "with a desktop head - or set PangeaOptions.Window.MainViewModelType.");
}
