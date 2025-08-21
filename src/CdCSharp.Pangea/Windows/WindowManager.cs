using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace CdCSharp.Pangea.Windows;


public interface IWindowManager : IDisposable
{
    Task<TWindow> ShowWindowAsync<TWindow, TViewModel>(NavigationParameter? navigationParameter = null)
        where TWindow : Window, new() where TViewModel : class;
    TWindow CreateWindow<TWindow, TViewModel>() where TWindow : Window, new() where TViewModel : class;
    TWindow CreateWindow<TWindow>() where TWindow : Window, new();
    void CloseWindow<TWindow>() where TWindow : Window;
    bool IsWindowOpen<TWindow>() where TWindow : Window;
    TWindow? GetWindow<TWindow>() where TWindow : Window;
    Window GetMainWindow();
    void SetMainWindow<TWindow, TViewModel>() where TWindow : Window, new() where TViewModel : class;
    void SetMainWindow(Window window);
}


public class WindowManager : IWindowManager
{
    private readonly ConcurrentDictionary<Type, WeakReference<Window>> _windowCache = new();
    private readonly SemaphoreSlim _creationSemaphore = new(1, 1);
    private readonly IServiceProvider _serviceProvider;
    private readonly IApplicationLifetime _applicationLifetime;
    private readonly PangeaOptions _options;
    private volatile bool _disposed;
    private Window? _mainWindow;

    public WindowManager(
        IServiceProvider serviceProvider, 
        IApplicationLifetime applicationLifetime,
        PangeaOptions options)
    {
        _serviceProvider = serviceProvider;
        _applicationLifetime = applicationLifetime;
        _options = options;
        
        if (_options.Window.AutoDiscoverMainWindow)
        {
            TryAutoInitializeMainWindow();
        }
    }

    private void TryAutoInitializeMainWindow()
    {
        Type? windowType = _options.Window.MainWindowType;
        Type? viewModelType = _options.Window.MainViewModelType;

        if (windowType == null || viewModelType == null)
        {
            windowType ??= TypeRegistry.Instance.GetType("MainWindow");
            viewModelType ??= TypeRegistry.Instance.GetType("MainWindowViewModel");
            
            if (windowType == null)
            {
                Type[] windowTypes = TypeRegistry.Instance.FindTypes("Window").ToArray();
                windowType = windowTypes.FirstOrDefault(t => t.Name.Contains("Main"));
            }
            
            if (viewModelType == null)
            {
                Type[] viewModelTypes = TypeRegistry.Instance.GetTypesDerivedFrom<ViewModelBase>().ToArray();
                viewModelType = viewModelTypes.FirstOrDefault(vm => vm.Name.Contains("Main"));
            }
        }

        if (windowType != null && viewModelType != null)
        {
            try
            {
                object viewModel = _serviceProvider.GetRequiredService(viewModelType);
                Window window = (Window?)Activator.CreateInstance(windowType) ??
                             throw new InvalidOperationException($"Unable to instantiate Window of type {windowType.Name}");
                window.DataContext = viewModel;
                
                SetMainWindowInternal(window);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to auto-initialize main window: {ex.Message}");
            }
        }
    }

    public void SetMainWindow<TWindow, TViewModel>() 
        where TWindow : Window, new() 
        where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));

        TViewModel viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        TWindow window = new();
        window.DataContext = viewModel;
        
        SetMainWindowInternal(window);
    }

    public void SetMainWindow(Window window)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        ArgumentNullException.ThrowIfNull(window);
        
        SetMainWindowInternal(window);
    }

    private void SetMainWindowInternal(Window window)
    {
        _mainWindow = window;
        SetMainWindowForLifetime(_applicationLifetime, _mainWindow);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ClearCache();
        _creationSemaphore.Dispose();
        _mainWindow = null;

        GC.SuppressFinalize(this);
    }

    public async Task<TWindow> ShowWindowAsync<TWindow, TViewModel>(NavigationParameter? navigationParameter = null)
        where TWindow : Window, new()
        where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));

        await _creationSemaphore.WaitAsync();
        try
        {
            if (TryGetCachedWindow<TWindow>(out TWindow? existingWindow))
                return await ActivateWindowAsync(existingWindow);

            return await CreateAndShowWindowAsync<TWindow, TViewModel>(navigationParameter);
        }
        finally
        {
            _creationSemaphore.Release();
        }
    }

    public TWindow CreateWindow<TWindow, TViewModel>()
        where TWindow : Window, new()
        where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        
        TViewModel viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        TWindow window = new();
        window.DataContext = viewModel;
        return window;
    }

    public TWindow CreateWindow<TWindow>() where TWindow : Window, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        return new TWindow();
    }

    public void CloseWindow<TWindow>() where TWindow : Window
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));

        if (!TryGetCachedWindow<TWindow>(out TWindow? window)) return;

        try
        {
            if (Dispatcher.UIThread.CheckAccess())
                window.Close();
            else
                Dispatcher.UIThread.InvokeAsync(() => window.Close()).Wait();

            RemoveFromCache<TWindow>();
        }
        catch
        {
            // Ignore close errors
        }
    }

    public bool IsWindowOpen<TWindow>() where TWindow : Window
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        return TryGetCachedWindow<TWindow>(out _);
    }

    public TWindow? GetWindow<TWindow>() where TWindow : Window
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        TryGetCachedWindow<TWindow>(out TWindow? window);
        return window;
    }

    public Window? GetMainWindow() => _mainWindow;

    private bool TryGetCachedWindow<TWindow>(out TWindow? window) where TWindow : Window
    {
        window = null;
        Type windowType = typeof(TWindow);

        if (!_windowCache.TryGetValue(windowType, out WeakReference<Window>? weakRef))
            return false;

        if (!weakRef.TryGetTarget(out Window? target) || target is not TWindow typedWindow)
        {
            _windowCache.TryRemove(windowType, out _);
            return false;
        }

        window = typedWindow;
        return true;
    }

    private void AddToCache<TWindow>(TWindow window) where TWindow : Window
    {
        Type windowType = typeof(TWindow);
        _windowCache[windowType] = new WeakReference<Window>(window);
        window.Closed += (_, _) => _windowCache.TryRemove(windowType, out _);
    }

    private void RemoveFromCache<TWindow>() where TWindow : Window
    {
        Type windowType = typeof(TWindow);
        _windowCache.TryRemove(windowType, out _);
    }

    private void ClearCache()
    {
        foreach (WeakReference<Window> weakRef in _windowCache.Values.ToList())
            if (weakRef.TryGetTarget(out Window? window))
                window.Close();

        _windowCache.Clear();
    }

    private async Task<TWindow> ActivateWindowAsync<TWindow>(TWindow window) where TWindow : Window
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
                window.Activate();
            else
                await Dispatcher.UIThread.InvokeAsync(() => window.Activate());

            return window;
        }
        catch
        {
            RemoveFromCache<TWindow>();
            throw;
        }
    }

    private async Task<TWindow> CreateAndShowWindowAsync<TWindow, TViewModel>(NavigationParameter? parameter)
        where TWindow : Window, new()
        where TViewModel : class
    {
        TWindow window = CreateWindow<TWindow, TViewModel>();

        if (Dispatcher.UIThread.CheckAccess())
            window.Show();
        else
            await Dispatcher.UIThread.InvokeAsync(() => window.Show());

        if (parameter != null && window.DataContext is INavigationAware navigationAware)
            await navigationAware.OnNavigatedToAsync(parameter);

        AddToCache(window);
        return window;
    }

    private void SetMainWindowForLifetime(IApplicationLifetime applicationLifetime, Window mainWindow)
    {
        switch (applicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = mainWindow;
                break;

            case ISingleViewApplicationLifetime singleView:
                if (mainWindow.Content is Control content)
                    singleView.MainView = content;
                else
                    throw new InvalidOperationException(
                        "For SingleView lifetime, MainWindow must have Content that inherits from Control");
                break;

            default:
                throw new InvalidOperationException($"Unsupported application lifetime: {applicationLifetime.GetType().Name}");
        }
    }
}