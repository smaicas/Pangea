using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Tests;

/// <summary>
/// The splash the toolkit ships, which is what an application that registers an initializer and
/// configures nothing else actually sees.
/// </summary>
/// <remarks>
/// The rest of the startup tests supply a splash of their own, because it is easier to assert
/// against. That leaves the one every application gets by default untested, which is the wrong way
/// round: a broken built-in splash is a broken first impression of the toolkit.
/// </remarks>
public class BuiltInSplashTests
{
    private sealed class ProbeViewModel;

    public sealed class ProbeMainWindow : Window;

    private sealed class SlowInitializer : IPangeaAsyncInitializer
    {
        private readonly Func<CancellationToken, Task> _work;

        public SlowInitializer(string name, Func<CancellationToken, Task> work)
        {
            Name = name;
            _work = work;
        }

        public string Name { get; }

        public int Order => 0;

        public Task InitializeAsync(CancellationToken cancellationToken) => _work(cancellationToken);
    }

    private sealed class ProbeApplication : PangeaApplication
    {
        private readonly IPangeaAsyncInitializer _initializer;
        private readonly Action<PangeaOptions>? _configureOptions;

        public ProbeApplication(IPangeaAsyncInitializer initializer, Action<PangeaOptions>? configureOptions = null)
        {
            _initializer = initializer;
            _configureOptions = configureOptions;
        }

        public override PangeaOptions ConfigurePangeaOptions(PangeaOptions options)
        {
            options.Window.MainWindowType = typeof(ProbeMainWindow);
            options.Window.MainViewModelType = typeof(ProbeViewModel);

            // Deliberately no SplashWindowType: this is the default an application gets.
            _configureOptions?.Invoke(options);
            return options;
        }

        public override void Configure(IServiceCollection services)
        {
            services.AddSingleton<ProbeViewModel>();
            services.AddSingleton(_initializer);
        }
    }

    private static IServiceProvider Bootstrap(
        PangeaApplication application, out ClassicDesktopStyleApplicationLifetime lifetime)
    {
        lifetime = new ClassicDesktopStyleApplicationLifetime();
        application.ApplicationLifetime = lifetime;

        return PangeaExtensions.ConfigureServices(application);
    }

    /// <summary>Every piece of text the splash is showing, read from the controls it was built from.</summary>
    private static IEnumerable<string> TextOf(Window window) =>
        window.Content is StackPanel panel
            ? panel.Children.OfType<TextBlock>().Select(block => block.Text ?? string.Empty)
            : [];

    [AvaloniaFact]
    public async Task TheDefaultSplashIsShownAndThenClosed()
    {
        TaskCompletionSource started = new();
        TaskCompletionSource release = new();

        IServiceProvider services = Bootstrap(
            new ProbeApplication(new SlowInitializer("Loading", async _ =>
            {
                started.SetResult();
                await release.Task;
            })),
            out _);

        (Task startup, Window? splash) = StartupSequence.Begin(services);

        await started.Task;

        // While the work runs, the splash is what the user is looking at.
        PangeaSplashWindow shown = Assert.IsType<PangeaSplashWindow>(splash);

        Assert.True(shown.IsVisible);
        Assert.Contains("Loading", TextOf(shown));

        release.SetResult();
        await startup;

        Assert.False(shown.IsVisible, "The splash should be closed once the main window is up.");
    }

    /// <summary>
    /// The failure path of the shipped splash: it stays open, says why, and grows the button that
    /// ends an application which now has nothing else on screen.
    /// </summary>
    [AvaloniaFact]
    public async Task AFailureTurnsTheDefaultSplashIntoTheReport()
    {
        IServiceProvider services = Bootstrap(
            new ProbeApplication(new SlowInitializer(
                "Opening the database",
                _ => Task.FromException(new InvalidOperationException("the database is locked")))),
            out _);

        (Task startup, Window? splash) = StartupSequence.Begin(services);
        await startup;

        PangeaSplashWindow report = Assert.IsType<PangeaSplashWindow>(splash);

        Assert.True(report.IsVisible, "The failure report is the only window left; closing it would leave nothing.");
        Assert.Contains("the database is locked", TextOf(report));

        Assert.True(
            report.Content is StackPanel panel && panel.Children.OfType<Button>().Any(),
            "A failed startup leaves no other way out, so the report has to offer one.");
    }

    /// <summary>
    /// A timeout arrives as a cancellation, whose own message names neither the initializer nor the
    /// limit. What the splash shows has to say both, because it is all anyone will see.
    /// </summary>
    [AvaloniaFact]
    public async Task AnInitializerThatRunsPastTheTimeout_IsReportedByName()
    {
        IServiceProvider services = Bootstrap(
            new ProbeApplication(
                new SlowInitializer("Warming the cache", token => Task.Delay(Timeout.Infinite, token)),
                options => options.Startup.Timeout = TimeSpan.FromMilliseconds(150)),
            out _);

        (Task startup, Window? splash) = StartupSequence.Begin(services);
        await startup;

        PangeaSplashWindow report = Assert.IsType<PangeaSplashWindow>(splash);

        string reported = Assert.Single(TextOf(report), text => text.Contains("gave up", StringComparison.Ordinal));

        Assert.Contains("Warming the cache", reported, StringComparison.Ordinal);
        Assert.DoesNotContain("A task was canceled", reported, StringComparison.Ordinal);
    }

    /// <summary>
    /// With <see cref="StartupFailureBehavior.Throw"/> a timeout leaves startup as a
    /// <see cref="TimeoutException"/> rather than as the cancellation underneath it, so whatever
    /// catches it can tell a slow start from an abandoned one.
    /// </summary>
    [AvaloniaFact]
    public async Task WithThrow_ATimeoutLeavesStartupAsATimeout()
    {
        IServiceProvider services = Bootstrap(
            new ProbeApplication(
                new SlowInitializer("Warming the cache", token => Task.Delay(Timeout.Infinite, token)),
                options =>
                {
                    options.Startup.Timeout = TimeSpan.FromMilliseconds(150);
                    options.Startup.FailureBehavior = StartupFailureBehavior.Throw;
                }),
            out _);

        TimeoutException failure =
            await Assert.ThrowsAsync<TimeoutException>(() => StartupSequence.RunAsync(services));

        Assert.Contains("Warming the cache", failure.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<OperationCanceledException>(failure.InnerException);
    }
}
