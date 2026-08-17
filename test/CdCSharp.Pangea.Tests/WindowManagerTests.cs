using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Tests;

/// <summary>
/// Window creation, caching and lifetime. The cache is what makes "give me the settings window"
/// return the same window twice, so its behaviour under concurrency is the interesting part.
/// </summary>
public class WindowManagerTests
{
    public sealed class SampleWindow : Window;

    public sealed class OtherWindow : Window;

    public sealed class SampleViewModel;

    private static WindowManager Create(PangeaOptions? options = null) =>
        new(new StubServices(),
            new ClassicDesktopStyleApplicationLifetime(),
            Options.Create(options ?? new PangeaOptions { Window = { AutoDiscoverMainWindow = false } }),
            new TypeRegistry(),
            NullLogger<WindowManager>.Instance);

    /// <summary>
    /// Whether Avalonia's dispatcher refuses work from other threads on this platform.
    /// </summary>
    /// <remarks>
    /// The headless dispatcher has thread affinity on Windows and not on the Linux runner. Where it
    /// has none, a call that marshals is indistinguishable from one that does not - it runs inline
    /// either way - so "this call waited for the UI thread" is simply not observable there. The
    /// window manager still talks to Avalonia's dispatcher directly instead of through
    /// IUIDispatcher, which is what would make this injectable and the same everywhere.
    /// </remarks>
    private static bool DispatcherHasThreadAffinity()
    {
        bool otherThreadWasAccepted = false;

        Thread probe = new(() => otherThreadWasAccepted = Dispatcher.UIThread.CheckAccess());
        probe.Start();
        probe.Join();

        return !otherThreadWasAccepted;
    }

    /// <summary>Pumps the dispatcher until <paramref name="work"/> finishes.</summary>
    /// <remarks>
    /// The test body owns the UI thread, so anything hopping onto it from a background thread only
    /// runs while we keep pumping. A real application's message loop does this for us.
    /// </remarks>
    private static void PumpUntilComplete(params Task[] work)
    {
        while (!work.All(task => task.IsCompleted))
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }
    }

    [AvaloniaFact]
    public void GetOrCreateWindow_ReturnsTheSameInstanceOnEveryCall()
    {
        WindowManager manager = Create();

        Assert.Same(manager.GetOrCreateWindow<SampleWindow>(), manager.GetOrCreateWindow<SampleWindow>());
    }

    [AvaloniaFact]
    public void GetOrCreateWindow_KeepsWindowTypesApart()
    {
        WindowManager manager = Create();

        Assert.NotSame((Window)manager.GetOrCreateWindow<SampleWindow>(), manager.GetOrCreateWindow<OtherWindow>());
    }

    [AvaloniaFact]
    public void ConcurrentCallers_GetTheSameWindow()
    {
        // Both callers miss the cache and race to build one; only one window may survive.
        WindowManager manager = Create();

        Task<SampleWindow> first = Task.Run(() => manager.GetOrCreateWindow<SampleWindow>());
        Task<SampleWindow> second = Task.Run(() => manager.GetOrCreateWindow<SampleWindow>());
        PumpUntilComplete(first, second);

        Assert.Same(first.Result, second.Result);
        Assert.Same(first.Result, manager.GetOrCreateWindow<SampleWindow>());
    }

    [AvaloniaFact]
    public void ClosingAWindow_LetsTheNextCallCreateAFreshOne()
    {
        WindowManager manager = Create();

        SampleWindow first = manager.GetOrCreateWindow<SampleWindow>();
        first.Show();
        first.Close();

        SampleWindow second = manager.GetOrCreateWindow<SampleWindow>();

        // Handing back a closed window would throw "Cannot re-show a closed window" on Show().
        Assert.NotSame(first, second);
        second.Show();
    }

    [AvaloniaFact]
    public void GetOrCreateWindow_WithAViewModel_SetsItAsTheDataContext()
    {
        WindowManager manager = Create();

        SampleWindow window = manager.GetOrCreateWindow<SampleWindow, SampleViewModel>();

        Assert.IsType<SampleViewModel>(window.DataContext);
    }

    [AvaloniaFact]
    public void SetMainWindow_PublishesItToTheDesktopLifetime()
    {
        ClassicDesktopStyleApplicationLifetime lifetime = new();
        WindowManager manager = new(
            new StubServices(),
            lifetime,
            Options.Create(new PangeaOptions { Window = { AutoDiscoverMainWindow = false } }),
            new TypeRegistry(),
            NullLogger<WindowManager>.Instance);

        SampleWindow window = new();
        manager.SetMainWindow(window);

        Assert.Same(window, lifetime.MainWindow);
        Assert.Same(window, manager.GetMainWindow());
    }

    [AvaloniaFact]
    public void CloseWindow_ClosesItAndForgetsIt()
    {
        WindowManager manager = Create();
        SampleWindow window = manager.GetOrCreateWindow<SampleWindow>();
        window.Show();

        manager.CloseWindow<SampleWindow>();

        Assert.False(window.IsVisible);
        Assert.NotSame(window, manager.GetOrCreateWindow<SampleWindow>());
    }

    [AvaloniaFact]
    public void CloseAllWindows_LeavesTheMainWindowAlone()
    {
        WindowManager manager = Create();
        SampleWindow main = new();
        manager.SetMainWindow(main);
        main.Show();

        OtherWindow secondary = manager.GetOrCreateWindow<OtherWindow>();
        secondary.Show();

        manager.CloseAllWindows();

        Assert.False(secondary.IsVisible);
        Assert.True(main.IsVisible);
    }

    /// <summary>
    /// Nothing pumps the UI thread while the test body holds it, so a call that genuinely marshals
    /// cannot finish until we pump. A call that posts and walks away finishes immediately - which
    /// is the difference between "the window is closed when this returns" and a promise.
    /// </summary>
    [AvaloniaFact]
    public void CloseWindow_FromABackgroundThread_DoesNotReturnUntilTheWindowIsClosed()
    {
        WindowManager manager = Create();
        SampleWindow window = manager.GetOrCreateWindow<SampleWindow>();
        window.Show();

        Assert.SkipUnless(DispatcherHasThreadAffinity(),
            "This platform's dispatcher accepts work from any thread, so waiting for it is not observable.");

        Task closing = Task.Run(manager.CloseWindow<SampleWindow>);

        Assert.False(closing.Wait(TimeSpan.FromMilliseconds(250)),
            "CloseWindow returned before the UI thread had a chance to close the window.");

        PumpUntilComplete(closing);
        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public void CloseAllWindows_FromABackgroundThread_DoesNotReturnUntilTheWindowsAreClosed()
    {
        WindowManager manager = Create();
        SampleWindow window = manager.GetOrCreateWindow<SampleWindow>();
        window.Show();

        Assert.SkipUnless(DispatcherHasThreadAffinity(),
            "This platform's dispatcher accepts work from any thread, so waiting for it is not observable.");

        Task closing = Task.Run(manager.CloseAllWindows);

        Assert.False(closing.Wait(TimeSpan.FromMilliseconds(250)),
            "CloseAllWindows returned before the UI thread had a chance to close anything.");

        PumpUntilComplete(closing);
        Assert.False(window.IsVisible);
    }

    /// <summary>
    /// A modal dialog needs an owner. Without a main window there is none, and the caller deserves
    /// to be told which call is missing rather than an exception from inside Avalonia.
    /// </summary>
    [AvaloniaFact]
    public async Task ShowDialog_WithoutAMainWindow_ExplainsWhatIsMissing()
    {
        WindowManager manager = Create();

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.ShowDialogAsync<SampleWindow, SampleViewModel>());

        Assert.Contains("SetMainWindow", error.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task ShowDialog_WhenTheActionFails_ClosesTheDialogAndSurfacesTheOriginalFailure()
    {
        WindowManager manager = Create();
        manager.SetMainWindow(new SampleWindow());
        manager.GetMainWindow()!.Show();

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.ShowDialogAsync<SampleWindow, SampleViewModel, int>(
                _ => throw new InvalidOperationException("the action failed")));

        // The failure the caller gets has to be their own, not whatever the abandoned ShowDialog
        // task turned into on its way out.
        Assert.Equal("the action failed", error.Message);
    }

    [AvaloniaFact]
    public void AfterDispose_EveryOperationIsRejected()
    {
        WindowManager manager = Create();
        manager.Dispose();

        Assert.Throws<ObjectDisposedException>(() => manager.GetOrCreateWindow<SampleWindow>());
        Assert.Throws<ObjectDisposedException>(manager.GetMainWindow);
        Assert.Throws<ObjectDisposedException>(manager.CloseAllWindows);
    }

    [AvaloniaFact]
    public void Dispose_IsIdempotent()
    {
        WindowManager manager = Create();

        manager.Dispose();
        manager.Dispose();
    }

    /// <summary>Resolves view models the way the container would, by constructing them.</summary>
    private sealed class StubServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => Activator.CreateInstance(serviceType);
    }
}
