using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Shell;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Tests;

/// <summary>
/// Starting on a platform that has no windows: Android, iOS, the browser.
/// </summary>
/// <remarks>
/// The rule under all of these is one thing: a <see cref="Window"/> is never constructed. Those
/// platforms register no windowing platform, so building one throws - which is why the shell, the
/// splash and every dialog are controls layered over a single view instead.
/// </remarks>
public class SingleViewShellTests
{
    /// <summary>The shell of an application written for a phone: a control, not a window.</summary>
    public sealed class MainView : UserControl;

    public sealed class MainViewModel;

    /// <summary>
    /// Stands in for what a phone shows. Avalonia's own single-view lifetime cannot be implemented
    /// outside Avalonia, which is exactly why the shell depends on this seam instead.
    /// </summary>
    private sealed class ProbeSurface : ISingleViewSurface
    {
        public Control? MainView { get; set; }
    }

    private sealed class BlockingInitializer : IPangeaAsyncInitializer
    {
        private readonly TaskCompletionSource _release;

        public BlockingInitializer(TaskCompletionSource release, string name)
        {
            _release = release;
            Name = name;
        }

        public string Name { get; }

        public Task InitializeAsync(CancellationToken cancellationToken) => _release.Task;
    }

    private sealed class ProbeApplication : PangeaApplication
    {
        private readonly IPangeaAsyncInitializer[] _initializers;
        private readonly ISingleViewSurface _surface;

        public ProbeApplication(ISingleViewSurface surface, params IPangeaAsyncInitializer[] initializers)
        {
            _surface = surface;
            _initializers = initializers;
        }

        public override PangeaOptions ConfigurePangeaOptions(PangeaOptions options)
        {
            options.Window.MainViewType = typeof(MainView);
            options.Window.MainViewModelType = typeof(MainViewModel);

            // Nothing here has a main window, and the suite's other probes leave window types in
            // this assembly for discovery to find.
            options.Window.AutoDiscoverMainWindow = false;
            return options;
        }

        /// <summary>
        /// Registered over the one the toolkit chose, which is the last word: the container takes
        /// the last registration, and the lifetime a headless session provides is a desktop one.
        /// </summary>
        public override void Configure(IServiceCollection services)
        {
            services.AddSingleton<MainViewModel>();

            services.AddSingleton<IShellPresenter>(provider => new SingleViewShellPresenter(
                provider,
                _surface,
                provider.GetRequiredService<IOptions<PangeaOptions>>(),
                provider.GetRequiredService<TypeRegistry>(),
                provider.GetRequiredService<ILogger<SingleViewShellPresenter>>(),
                provider.GetService<PangeaCatalogIndex>()));

            foreach (IPangeaAsyncInitializer initializer in _initializers) services.AddSingleton(initializer);
        }
    }

    private static IServiceProvider Bootstrap(
        out ProbeSurface surface, params IPangeaAsyncInitializer[] initializers)
    {
        surface = new ProbeSurface();

        ProbeApplication application = new(surface, initializers)
        {
            ApplicationLifetime = new ClassicDesktopStyleApplicationLifetime()
        };

        return PangeaExtensions.ConfigureServices(application);
    }

    private static PangeaShellHost HostOf(ProbeSurface surface) =>
        Assert.IsType<PangeaShellHost>(surface.MainView);

    [AvaloniaFact]
    public void TheShellPresenterFollowsTheLifetime()
    {
        IServiceProvider services = Bootstrap(out _);

        Assert.True(services.GetRequiredService<IShellPresenter>().IsSingleView);
    }

    /// <summary>
    /// What the lifetime is given is the host, not the application's own view: the splash and every
    /// dialog need somewhere to be layered, and there is no second view to put them in.
    /// </summary>
    [AvaloniaFact]
    public async Task TheMainViewIsShownInsideTheShellHost()
    {
        IServiceProvider services = Bootstrap(out ProbeSurface surface);

        await StartupSequence.RunAsync(services);

        Assert.IsType<MainView>(HostOf(surface).MainContent);
    }

    [AvaloniaFact]
    public async Task TheMainViewIsGivenItsViewModel()
    {
        IServiceProvider services = Bootstrap(out ProbeSurface surface);

        await StartupSequence.RunAsync(services);

        Assert.IsType<MainViewModel>(HostOf(surface).MainContent?.DataContext);
    }

    /// <summary>
    /// The splash is an overlay while the work runs, and the main view arrives underneath it before
    /// it goes - so nothing between the two is ever the blank screen the user would otherwise see.
    /// </summary>
    [AvaloniaFact]
    public async Task TheSplashIsLayeredOverTheShellAndThenRemoved()
    {
        TaskCompletionSource release = new();

        IServiceProvider services = Bootstrap(out ProbeSurface surface,
            new BlockingInitializer(release, "Loading"));

        (Task startup, Window? splashWindow) = StartupSequence.Begin(services);

        Assert.Null(splashWindow);

        PangeaShellHost host = HostOf(surface);
        Assert.Single(host.Overlays);
        Assert.IsType<PangeaSplashView>(host.Overlays[0]);

        release.SetResult();
        await startup;

        Assert.Empty(host.Overlays);
        Assert.IsType<MainView>(host.MainContent);
    }

    /// <summary>
    /// A failed startup keeps the report on screen: there is no other view to fall back to, and
    /// closing the only one would leave the user with no reason given.
    /// </summary>
    [AvaloniaFact]
    public async Task AFailedStartupLeavesTheReportOnScreen()
    {
        IServiceProvider services = Bootstrap(out ProbeSurface surface,
            new FailingInitializer("Opening the database", new InvalidOperationException("the database is locked")));

        await StartupSequence.RunAsync(services);

        PangeaShellHost host = HostOf(surface);

        Assert.Single(host.Overlays);
        Assert.Null(host.MainContent);
    }

    private sealed class FailingInitializer : IPangeaAsyncInitializer
    {
        private readonly Exception _failure;

        public FailingInitializer(string name, Exception failure)
        {
            Name = name;
            _failure = failure;
        }

        public string Name { get; }

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.FromException(_failure);
    }

    /// <summary>
    /// A dialog with no window to open: the card is layered over the shell, and the caller waits on
    /// the button the same way it waits on a modal window elsewhere.
    /// </summary>
    [AvaloniaFact]
    public async Task ADialogIsLayeredOverTheShellAndAnswersTheCaller()
    {
        IServiceProvider services = Bootstrap(out ProbeSurface surface);

        await StartupSequence.RunAsync(services);

        MessageDialogView? shown = null;

        void OnCreated(MessageDialogView dialog) => shown = dialog;

        MessageDialogView.Created += OnCreated;

        try
        {
            Task<bool> answer = services.GetRequiredService<IDialogService>()
                .ConfirmAsync("Delete", "Delete the expense?", "Delete it", "Keep it");

            PangeaShellHost host = HostOf(surface);

            Assert.NotNull(shown);
            Assert.Contains(shown!, host.Overlays);
            Assert.Equal("Delete it", shown!.ConfirmButton.Content);
            Assert.Equal("Keep it", shown.CancelButton!.Content);

            shown.ConfirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.True(await answer);
            Assert.Empty(host.Overlays);
        }
        finally
        {
            MessageDialogView.Created -= OnCreated;
        }
    }

    [AvaloniaFact]
    public async Task DismissingADialogIsReadAsACancel()
    {
        IServiceProvider services = Bootstrap(out _);

        await StartupSequence.RunAsync(services);

        MessageDialogView? shown = null;

        void OnCreated(MessageDialogView dialog) => shown = dialog;

        MessageDialogView.Created += OnCreated;

        try
        {
            Task<bool> answer = services.GetRequiredService<IDialogService>().ConfirmAsync("Delete", "Sure?");

            Assert.NotNull(shown);
            shown!.CancelButton!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.False(await answer);
        }
        finally
        {
            MessageDialogView.Created -= OnCreated;
        }
    }

    /// <summary>An alert has nothing to cancel, here as anywhere else.</summary>
    [AvaloniaFact]
    public async Task AnAlertHasNoCancelButton()
    {
        IServiceProvider services = Bootstrap(out _);

        await StartupSequence.RunAsync(services);

        MessageDialogView? shown = null;

        void OnCreated(MessageDialogView dialog) => shown = dialog;

        MessageDialogView.Created += OnCreated;

        try
        {
            Task pending = services.GetRequiredService<IDialogService>().AlertAsync("Saved", "Your changes are saved.");

            Assert.NotNull(shown);
            Assert.Null(shown!.CancelButton);

            shown.ConfirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await pending;
        }
        finally
        {
            MessageDialogView.Created -= OnCreated;
        }
    }

    /// <summary>
    /// An application whose splash was written for desktop still starts: a Window cannot be opened
    /// here at all, so the built-in view stands in rather than the platform taking the process down.
    /// </summary>
    [AvaloniaFact]
    public void AWindowSplashFallsBackToTheBuiltInView()
    {
        IServiceProvider services = Bootstrap(out ProbeSurface surface);

        PangeaStartupOptions options = new() { SplashWindowType = typeof(Window) };

        Control? splash = services.GetRequiredService<IShellPresenter>().ShowSplash(options);

        Assert.IsType<PangeaSplashView>(splash);
        Assert.Single(HostOf(surface).Overlays);
    }
}
