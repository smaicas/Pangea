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
