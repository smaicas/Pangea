using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Tests;

/// <summary>
/// What happens between the container being built and the main window appearing, once the
/// application has work that has to finish first.
/// </summary>
/// <remarks>
/// The interesting cases are the ones an application only meets on someone else's machine: an
/// initializer that fails, and the order two of them run in.
/// </remarks>
public class StartupSequenceTests
{
    public sealed class ProbeMainWindow : Window
    {
        public static int Created { get; private set; }

        public static ProbeMainWindow? Last { get; private set; }

        public ProbeMainWindow()
        {
            Created++;
            Last = this;
        }

        public static void Reset()
        {
            Created = 0;
            Last = null;
        }
    }

    public sealed class ProbeViewModel;

    /// <summary>A splash the test can read: what it was told, and whether it is still open.</summary>
    public sealed class ProbeSplashWindow : Window, IPangeaSplashView
    {
        public static ProbeSplashWindow? Last { get; private set; }

        public ProbeSplashWindow() => Last = this;

        public List<string> Statuses { get; } = [];

        public string? Failure { get; private set; }

        public bool WasClosed { get; private set; }

        public void ReportStatus(string status) => Statuses.Add(status);

        public void ReportFailure(string message) => Failure = message;

        protected override void OnClosed(EventArgs e)
        {
            WasClosed = true;
            base.OnClosed(e);
        }

        public static void Reset() => Last = null;
    }

    private sealed class RecordingInitializer : IPangeaAsyncInitializer
    {
        private readonly List<string> _log;
        private readonly Exception? _failure;

        public RecordingInitializer(List<string> log, string name, int order = 0, Exception? failure = null)
        {
            _log = log;
            Name = name;
            Order = order;
            _failure = failure;
        }

        public string Name { get; }

        public int Order { get; }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            _log.Add(Name);
            return _failure is null ? Task.CompletedTask : Task.FromException(_failure);
        }
    }

    private sealed class ProbeApplication : PangeaApplication
    {
        private readonly IPangeaAsyncInitializer[] _initializers;
        private readonly Action<PangeaOptions>? _configureOptions;

        public ProbeApplication(Action<PangeaOptions>? configureOptions, params IPangeaAsyncInitializer[] initializers)
        {
            _configureOptions = configureOptions;
            _initializers = initializers;
        }

        public override PangeaOptions ConfigurePangeaOptions(PangeaOptions options)
        {
            options.Window.MainWindowType = typeof(ProbeMainWindow);
            options.Window.MainViewModelType = typeof(ProbeViewModel);
            options.Startup.SplashWindowType = typeof(ProbeSplashWindow);

            _configureOptions?.Invoke(options);
            return options;
        }

        public override void Configure(IServiceCollection services)
        {
            services.AddSingleton<ProbeViewModel>();

            foreach (IPangeaAsyncInitializer initializer in _initializers)
            {
                services.AddSingleton(initializer);
            }
        }
    }

    private static IServiceProvider Bootstrap(PangeaApplication application) => Bootstrap(application, out _);

    private static IServiceProvider Bootstrap(
        PangeaApplication application, out ClassicDesktopStyleApplicationLifetime lifetime)
    {
        ProbeMainWindow.Reset();
        ProbeSplashWindow.Reset();

        lifetime = new ClassicDesktopStyleApplicationLifetime();
        application.ApplicationLifetime = lifetime;

        return PangeaExtensions.ConfigureServices(application);
    }

    /// <summary>
    /// An application with nothing to wait for starts the way it always did: no splash is built,
    /// and the main window is up before the call returns.
    /// </summary>
    [AvaloniaFact]
    public async Task WithNoInitializers_TheMainWindowIsShownStraightAway()
    {
        IServiceProvider services = Bootstrap(new ProbeApplication(configureOptions: null));

        await StartupSequence.RunAsync(services);

        Assert.Null(ProbeSplashWindow.Last);
        Assert.True(ProbeMainWindow.Last?.IsVisible);
    }

    [AvaloniaFact]
    public async Task Initializers_RunInOrder_AndTheMainWindowReplacesTheSplash()
    {
        List<string> log = [];

        IServiceProvider services = Bootstrap(new ProbeApplication(
            configureOptions: null,
            new RecordingInitializer(log, "second", order: 10),
            new RecordingInitializer(log, "first", order: -10)));

        await StartupSequence.RunAsync(services);

        Assert.Equal(["first", "second"], log);

        ProbeSplashWindow splash = Assert.IsType<ProbeSplashWindow>(ProbeSplashWindow.Last);

        // Each one says what it is doing while it does it.
        Assert.Equal(["first", "second"], splash.Statuses);
        Assert.Null(splash.Failure);
        Assert.True(splash.WasClosed, "The splash should be gone once the main window is up.");
        Assert.True(ProbeMainWindow.Last?.IsVisible);
    }

    /// <summary>
    /// The default when an initializer fails: the splash becomes the failure report and the main
    /// window is never built. An application whose database did not open has nothing to show, and
    /// a process that vanishes with no window and no message is worse than one that says why.
    /// </summary>
    [AvaloniaFact]
    public async Task AFailedInitializer_IsReportedOnTheSplash_AndNoMainWindowAppears()
    {
        List<string> log = [];

        IServiceProvider services = Bootstrap(new ProbeApplication(
            configureOptions: null,
            new RecordingInitializer(log, "broken", failure: new InvalidOperationException("the database is locked"))));

        await StartupSequence.RunAsync(services);

        ProbeSplashWindow splash = Assert.IsType<ProbeSplashWindow>(ProbeSplashWindow.Last);

        Assert.Equal("the database is locked", splash.Failure);
        Assert.False(splash.WasClosed);
        Assert.Equal(0, ProbeMainWindow.Created);
    }

    /// <summary>The innermost message, not the wrapper: that is the one that says what happened.</summary>
    [AvaloniaFact]
    public async Task TheReportedMessageIsTheInnermostOne()
    {
        List<string> log = [];

        Exception failure = new InvalidOperationException(
            "Migrating 'AppDbContext' failed.", new IOException("the file is in use"));

        IServiceProvider services = Bootstrap(new ProbeApplication(
            configureOptions: null, new RecordingInitializer(log, "broken", failure: failure)));

        await StartupSequence.RunAsync(services);

        Assert.Equal("the file is in use", ProbeSplashWindow.Last?.Failure);
    }

    [AvaloniaFact]
    public async Task WithContinue_AFailureIsLoggedAndTheApplicationStartsAnyway()
    {
        List<string> log = [];

        IServiceProvider services = Bootstrap(new ProbeApplication(
            options => options.Startup.FailureBehavior = StartupFailureBehavior.Continue,
            new RecordingInitializer(log, "optional", failure: new InvalidOperationException("no matter"))));

        await StartupSequence.RunAsync(services);

        Assert.True(ProbeMainWindow.Last?.IsVisible);
        Assert.True(ProbeSplashWindow.Last?.WasClosed);
    }

    [AvaloniaFact]
    public async Task WithThrow_TheFailureLeavesStartup()
    {
        List<string> log = [];

        IServiceProvider services = Bootstrap(new ProbeApplication(
            options => options.Startup.FailureBehavior = StartupFailureBehavior.Throw,
            new RecordingInitializer(log, "fatal", failure: new InvalidOperationException("unrecoverable"))));

        InvalidOperationException error =
            await Assert.ThrowsAsync<InvalidOperationException>(() => StartupSequence.RunAsync(services));

        Assert.Equal("unrecoverable", error.Message);
        Assert.Equal(0, ProbeMainWindow.Created);
    }

    /// <summary>
    /// With no splash there is nothing to report a failure on, so the failure is left to the
    /// caller rather than swallowed in front of an empty screen.
    /// </summary>
    [AvaloniaFact]
    public async Task WithoutASplash_AFailureIsNotReportedQuietly()
    {
        List<string> log = [];

        IServiceProvider services = Bootstrap(new ProbeApplication(
            options => options.Startup.ShowSplash = false,
            new RecordingInitializer(log, "broken", failure: new InvalidOperationException("nowhere to say so"))));

        await Assert.ThrowsAsync<InvalidOperationException>(() => StartupSequence.RunAsync(services));

        Assert.Null(ProbeSplashWindow.Last);
    }

    /// <summary>
    /// Records what was posted to the UI thread instead of running it, which is the only way to
    /// watch a rethrow without taking the test run down with it.
    /// </summary>
    private sealed class CapturingDispatcher : IUIDispatcher
    {
        public List<Action> Posted { get; } = [];

        public bool CheckAccess() => true;

        public void Post(Action action) => Posted.Add(action);

        public void Invoke(Action action) => action();

        public T Invoke<T>(Func<T> callback) => callback();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<Task<T>> callback) => callback();
    }

    /// <summary>
    /// Startup is started and not awaited, so without this a failure it chose to let out - which is
    /// what <see cref="StartupFailureBehavior.Throw"/> is - would be swallowed by the abandoned
    /// task and surface, if at all, as an unobserved exception long afterwards.
    /// </summary>
    [Fact]
    public async Task AFailureNobodyIsAwaiting_IsRethrownOnTheUIThread()
    {
        InvalidOperationException failure = new("startup could not finish");
        CapturingDispatcher dispatcher = new();

        await StartupSequence.ObserveAsync(Task.FromException(failure), dispatcher);

        Action rethrow = Assert.Single(dispatcher.Posted);

        // The exception itself, not a copy or a wrapper: whatever handles it sees what was thrown.
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(rethrow));
    }

    [Fact]
    public async Task AStartupThatSucceeds_PostsNothing()
    {
        CapturingDispatcher dispatcher = new();

        await StartupSequence.ObserveAsync(Task.CompletedTask, dispatcher);

        Assert.Empty(dispatcher.Posted);
    }

    [AvaloniaFact]
    public async Task ASplashThatIsNotAWindow_IsRejectedByName()
    {
        List<string> log = [];

        IServiceProvider services = Bootstrap(new ProbeApplication(
            options => options.Startup.SplashWindowType = typeof(ProbeViewModel),
            new RecordingInitializer(log, "any")));

        InvalidOperationException error =
            await Assert.ThrowsAsync<InvalidOperationException>(() => StartupSequence.RunAsync(services));

        Assert.Contains(nameof(ProbeViewModel), error.Message, StringComparison.Ordinal);
    }
}
