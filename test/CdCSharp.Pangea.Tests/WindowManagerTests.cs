using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Testing.Dispatchers;
using CdCSharp.Pangea.Tests.Infrastructure;
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

    private static WindowManager Create(PangeaOptions? options = null) => Create(out _, options);

    private static WindowManager Create(out PumpingUIDispatcher dispatcher, PangeaOptions? options = null)
    {
        dispatcher = new PumpingUIDispatcher();

        return new WindowManager(
            new StubServices(),
            Options.Create(options ?? new PangeaOptions { Window = { AutoDiscoverMainWindow = false } }),
            new TypeRegistry(),
            dispatcher,
            NullLogger<WindowManager>.Instance);
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

    /// <summary>
    /// Both callers miss the cache and race to build a window; only one may survive. The dispatcher
    /// is what serialises them, so the test drives it rather than Avalonia's.
    /// </summary>
    [AvaloniaFact]
    public void ConcurrentCallers_GetTheSameWindow()
    {
        WindowManager manager = Create(out PumpingUIDispatcher dispatcher);

        SampleWindow? first = null;
        SampleWindow? second = null;

        Thread one = new(() => first = manager.GetOrCreateWindow<SampleWindow>());
        Thread two = new(() => second = manager.GetOrCreateWindow<SampleWindow>());

        one.Start();
        two.Start();

        dispatcher.DrainUntil(() => one.Join(1) && two.Join(1));

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Same(first, manager.GetOrCreateWindow<SampleWindow>());
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
            new StubServices(lifetime),
            Options.Create(new PangeaOptions { Window = { AutoDiscoverMainWindow = false } }),
            new TypeRegistry(),
            new PumpingUIDispatcher(),
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
    /// CloseWindow returns void, so the caller can only assume the window is closed when it
    /// returns. Posting the work and walking away breaks that quietly.
    /// </summary>
    [AvaloniaFact]
    public void CloseWindow_FromAnotherThread_DoesNotReturnUntilTheWindowIsClosed()
    {
        WindowManager manager = Create(out PumpingUIDispatcher dispatcher);
        SampleWindow window = manager.GetOrCreateWindow<SampleWindow>();
        window.Show();

        Thread worker = new(manager.CloseWindow<SampleWindow>);
        worker.Start();

        // Nothing has run the queued work, so a call that marshals cannot have returned.
        Assert.False(worker.Join(TimeSpan.FromMilliseconds(200)),
            "CloseWindow returned before the UI thread had run anything.");

        dispatcher.Drain();
        worker.Join();

        Assert.Equal(1, dispatcher.MarshalledCount);
        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public void CloseAllWindows_FromAnotherThread_DoesNotReturnUntilTheWindowsAreClosed()
    {
        WindowManager manager = Create(out PumpingUIDispatcher dispatcher);
        SampleWindow window = manager.GetOrCreateWindow<SampleWindow>();
        window.Show();

        Thread worker = new(manager.CloseAllWindows);
        worker.Start();

        Assert.False(worker.Join(TimeSpan.FromMilliseconds(200)),
            "CloseAllWindows returned before the UI thread had run anything.");

        dispatcher.Drain();
        worker.Join();

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

}
