using Avalonia.Controls;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.ExceptionServices;

namespace CdCSharp.Pangea.Startup;

/// <summary>
/// What happens between the container being built and the user seeing the main window.
/// </summary>
/// <remarks>
/// With no <see cref="IPangeaAsyncInitializer"/> registered this is the one line it always was:
/// create the main window and show it. With initializers registered, a splash window goes up first,
/// each initializer is awaited off the UI thread, and the main window replaces the splash when they
/// are done.
/// </remarks>
internal static class StartupSequence
{
    /// <summary>
    /// Starts the sequence and makes sure a failure in it is not lost.
    /// </summary>
    /// <remarks>
    /// The application cannot await startup - the UI thread has to go back to pumping messages or
    /// the splash never draws - and a task nobody awaits swallows whatever it throws until the
    /// finalizer notices. That is fine for the failures this handles for itself and wrong for the
    /// ones it deliberately does not:
    /// <see cref="StartupFailureBehavior.Throw"/> promises to take the process down, and so an
    /// unobserved task would make it the quietest of the three options rather than the loudest.
    /// </remarks>
    public static void Start(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        Task startup = RunAsync(serviceProvider);

        if (startup.IsCompletedSuccessfully) return;

        _ = ObserveAsync(startup, serviceProvider.GetRequiredService<IUIDispatcher>());
    }

    /// <summary>
    /// Rethrows a failed startup on the UI thread, where the application's own unhandled-exception
    /// handling can see it.
    /// </summary>
    internal static async Task ObserveAsync(Task startup, IUIDispatcher dispatcher)
    {
        try
        {
            await startup;
        }
        catch (Exception ex)
        {
            // Captured rather than wrapped, so what reaches the handler is the exception that was
            // thrown, with the stack it was thrown from.
            dispatcher.Post(() => ExceptionDispatchInfo.Capture(ex).Throw());
        }
    }

    /// <summary>
    /// Runs the sequence. Returns once the main window is up, or - when there are initializers to
    /// run - once the splash is up and the rest is in flight.
    /// </summary>
    /// <returns>
    /// The work still to be done, already running. Startup does not await it: the UI thread has to
    /// go back to pumping messages or the splash never draws. Tests await it.
    /// </returns>
    public static Task RunAsync(IServiceProvider serviceProvider) => Begin(serviceProvider).Work;

    /// <summary>
    /// The sequence, and the window standing in for the main one while it runs.
    /// </summary>
    /// <remarks>
    /// A splash belongs to nobody once it is shown: it is not the application's main window, and
    /// the lifetime a test builds by hand does not collect it. Handing it back is how the built-in
    /// one can be asserted against at all - which matters more than the custom one, because it is
    /// what an application that configures nothing actually shows.
    /// </remarks>
    internal static (Task Work, Window? Splash) Begin(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        List<IPangeaAsyncInitializer> initializers = serviceProvider
            .GetServices<IPangeaAsyncInitializer>()
            .OrderBy(initializer => initializer.Order)
            .ToList();

        if (initializers.Count == 0)
        {
            ShowMainWindow(serviceProvider);
            return (Task.CompletedTask, null);
        }

        PangeaStartupOptions options = serviceProvider
            .GetRequiredService<IOptions<PangeaOptions>>().Value.Startup;

        Window? splash = CreateSplash(serviceProvider, options);
        splash?.Show();

        return (InitializeAsync(serviceProvider, options, initializers, splash), splash);
    }

    private static async Task InitializeAsync(
        IServiceProvider serviceProvider,
        PangeaStartupOptions options,
        IReadOnlyList<IPangeaAsyncInitializer> initializers,
        Window? splash)
    {
        ILogger logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(StartupSequence));
        IPangeaSplashView? report = splash as IPangeaSplashView;

        using CancellationTokenSource cancellation = new();
        if (options.Timeout is { } timeout) cancellation.CancelAfter(timeout);

        IPangeaAsyncInitializer? running = null;

        try
        {
            foreach (IPangeaAsyncInitializer initializer in initializers)
            {
                running = initializer;
                report?.ReportStatus(initializer.Name);

                // Off the UI thread, and back on it to report: an initializer is chosen for work
                // that takes long enough to be worth a splash, which is exactly long enough to
                // freeze the window that is showing it.
                await Task.Run(() => initializer.InitializeAsync(cancellation.Token), cancellation.Token);

                logger.LogDebug("Startup initializer '{Initializer}' finished", initializer.Name);
            }
        }
        catch (Exception thrown)
        {
            Exception ex = Describe(thrown, running, options, cancellation);

            logger.LogError(ex, "Startup failed while running the initializers");

            switch (options.FailureBehavior)
            {
                case StartupFailureBehavior.Continue:
                    break;

                // Nowhere to report it: the application asked for no splash, so failing quietly
                // would leave a process running with no window and no reason given.
                case StartupFailureBehavior.Report when report is null:
                case StartupFailureBehavior.Throw:
                    // Rethrown as it was caught whenever nothing was added to it, so the stack the
                    // caller sees is the stack it was thrown from.
                    if (ReferenceEquals(ex, thrown)) throw;
                    throw ex;

                case StartupFailureBehavior.Report:
                    report.ReportFailure(Explain(ex));
                    return;
            }
        }

        ShowMainWindow(serviceProvider);

        // After the main window: closing the last window of a desktop application ends it, and for
        // a moment the splash is the last window.
        splash?.Close();
    }

    /// <summary>
    /// What actually failed, said in terms of startup.
    /// </summary>
    /// <remarks>
    /// A timeout arrives as a cancellation, whose message is "A task was canceled" - which names
    /// neither the initializer that ran long nor the limit it ran past, and is the one thing the
    /// person reading the splash needs to know.
    /// </remarks>
    private static Exception Describe(
        Exception thrown,
        IPangeaAsyncInitializer? running,
        PangeaStartupOptions options,
        CancellationTokenSource cancellation) =>
        thrown is OperationCanceledException && cancellation.IsCancellationRequested && options.Timeout is { } limit
            ? new TimeoutException(
                $"Startup gave up after {limit}: '{running?.Name ?? "an initializer"}' had not finished.", thrown)
            : thrown;

    /// <summary>
    /// What to put on the splash.
    /// </summary>
    /// <remarks>
    /// The innermost message, which is the one that says what actually went wrong: the outer frames
    /// of a startup failure are usually "an initializer threw" restated. The exception this class
    /// builds for a timeout is the exception: its own message is the explanation, and what it wraps
    /// is the cancellation that carries none.
    /// </remarks>
    private static string Explain(Exception exception)
    {
        if (exception is TimeoutException) return exception.Message;

        Exception innermost = exception;

        while (innermost.InnerException is { } inner) innermost = inner;

        return innermost.Message;
    }

    private static Window? CreateSplash(IServiceProvider serviceProvider, PangeaStartupOptions options)
    {
        if (!options.ShowSplash) return null;

        if (options.SplashWindowType is null) return new PangeaSplashWindow(options.SplashTitle);

        if (!typeof(Window).IsAssignableFrom(options.SplashWindowType))
        {
            throw new InvalidOperationException(
                $"'{options.SplashWindowType.FullName}' is configured as the splash window but does not derive from Window.");
        }

        // Through the container when it knows the type - a splash with a view model is still a
        // view - and by constructor otherwise.
        return (Window)(serviceProvider.GetService(options.SplashWindowType)
                        ?? Activator.CreateInstance(options.SplashWindowType)
                        ?? throw new InvalidOperationException(
                            $"The splash window '{options.SplashWindowType.FullName}' could not be created."));
    }

    private static void ShowMainWindow(IServiceProvider serviceProvider)
    {
        IWindowManager? windowManager = serviceProvider.GetService<IWindowManager>();

        if (windowManager is null) return;

        windowManager.Initialize();
        windowManager.GetMainWindow()?.Show();
    }
}
