using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace CdCSharp.Pangea.Windows;

public interface IWindowManager : IDisposable
{
    Task<TWindow> ShowWindowAsync<TWindow, TViewModel>()
        where TWindow : Window, new() where TViewModel : class;
    
    TWindow GetOrCreateWindow<TWindow, TViewModel>()
        where TWindow : Window, new() where TViewModel : class;
    
    TWindow GetOrCreateWindow<TWindow>() where TWindow : Window, new();
    
    void CloseWindow<TWindow>() where TWindow : Window;
    void CloseAllWindows();
    
    Window? GetMainWindow();
    void SetMainWindow<TWindow, TViewModel>() where TWindow : Window, new() where TViewModel : class;
    void SetMainWindow(Window window);
    void Initialize();

    /// <summary>
    /// Shows a modal dialog and runs <paramref name="dialogAction"/> against its view model.
    /// </summary>
    /// <exception cref="InvalidOperationException">No main window has been set to own the dialog.</exception>
    Task<TResult> ShowDialogAsync<TWindow, TViewModel, TResult>(
        Func<TViewModel, Task<TResult>> dialogAction)
        where TWindow : Window, new()
        where TViewModel : class;

    /// <summary>Shows a modal dialog and returns its result.</summary>
    /// <exception cref="InvalidOperationException">No main window has been set to own the dialog.</exception>
    Task<bool?> ShowDialogAsync<TWindow, TViewModel>()
        where TWindow : Window, new()
        where TViewModel : class;
}

public class WindowManager : IWindowManager, IDisposable
{
    private readonly ConcurrentDictionary<Type, WeakReference<Window>> _windowCache = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly TypeRegistry _typeRegistry;
    private readonly ILogger<WindowManager> _logger;
    private readonly IApplicationLifetime _applicationLifetime;
    private readonly IUIDispatcher _dispatcher;
    private readonly PangeaOptions _options;
    private Window? _mainWindow;
    private bool _initialized;
    private volatile bool _disposed;

    public WindowManager(
        IServiceProvider serviceProvider,
        IApplicationLifetime applicationLifetime,
        IOptions<PangeaOptions> options,
        TypeRegistry typeRegistry,
        IUIDispatcher dispatcher,
        ILogger<WindowManager> logger)
    {
        _serviceProvider = serviceProvider;
        _typeRegistry = typeRegistry;
        _logger = logger;
        _applicationLifetime = applicationLifetime;
        _dispatcher = dispatcher;
        _options = options.Value;
    }

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        
        if (_initialized) return;
        _initialized = true;

        if (_options.Window.AutoDiscoverMainWindow)
        {
            TryAutoInitializeMainWindow();
        }
    }

    public Window? GetMainWindow()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        Initialize();
        return _mainWindow;
    }

    public void SetMainWindow<TWindow, TViewModel>() 
        where TWindow : Window, new() 
        where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        
        _dispatcher.Invoke(SetMainWindowInternal<TWindow, TViewModel>);
    }

    public void SetMainWindow(Window window)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        ArgumentNullException.ThrowIfNull(window);
        
        _dispatcher.Invoke(() => SetMainWindowInternal(window));
    }

    public TWindow GetOrCreateWindow<TWindow, TViewModel>()
        where TWindow : Window, new()
        where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        
        if (TryGetCachedWindow<TWindow>(out TWindow? existingWindow))
            return existingWindow;

        // Re-check on the UI thread: without it two callers that both miss the cache each build a
        // window, and the second silently replaces the first in the cache.
        return _dispatcher.Invoke(() =>
            TryGetCachedWindow<TWindow>(out TWindow? cached)
                ? cached
                : CreateWindowWithViewModel<TWindow, TViewModel>());
    }

    public TWindow GetOrCreateWindow<TWindow>() where TWindow : Window, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));

        if (TryGetCachedWindow<TWindow>(out TWindow? existingWindow))
            return existingWindow;

        // Re-check on the UI thread: without it two callers that both miss the cache each build a
        // window, and the second silently replaces the first in the cache.
        return _dispatcher.Invoke(() =>
            TryGetCachedWindow<TWindow>(out TWindow? cached) ? cached : CreateWindow<TWindow>());
    }

    public async Task<TWindow> ShowWindowAsync<TWindow, TViewModel>()
        where TWindow : Window, new()
        where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));

        TWindow window = GetOrCreateWindow<TWindow, TViewModel>();

        await _dispatcher.InvokeAsync(() => ShowWindowSafe(window));

        return window;
    }

    public async Task<TResult> ShowDialogAsync<TWindow, TViewModel, TResult>(
        Func<TViewModel, Task<TResult>> dialogAction)
        where TWindow : Window, new() 
        where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        ArgumentNullException.ThrowIfNull(dialogAction);

        Window owner = RequireDialogOwner();

        // Dialogs are single-use, so they are never cached.
        TViewModel viewModel;
        TWindow window;

        (window, viewModel) = _dispatcher.Invoke(CreateDialogWindow<TWindow, TViewModel>);

        Task<bool?>? showing = null;

        try
        {
            await ConfigureAsModalDialog(window);

            // The dialog is shown while the action runs; both have to be awaited, or a failure
            // inside ShowDialog is never observed and the dialog outlives the call.
            showing = ShowDialogInternal(window, owner);
            TResult result = await dialogAction(viewModel);

            if (window.IsVisible)
            {
                await CloseDialogSafe(window);
            }

            await showing;
            return result;
        }
        catch (Exception)
        {
            if (window.IsVisible)
            {
                await CloseDialogSafe(window);
            }

            if (showing is not null)
            {
                await ObserveDialogFailure(showing);
            }

            throw;
        }
    }

    public async Task<bool?> ShowDialogAsync<TWindow, TViewModel>()
        where TWindow : Window, new() 
        where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));

        Window owner = RequireDialogOwner();

        // Dialogs are single-use, so they are never cached.
        TWindow window;

        (window, _) = _dispatcher.Invoke(CreateDialogWindow<TWindow, TViewModel>);

        try
        {
            await ConfigureAsModalDialog(window);
            return await ShowDialogInternal(window, owner);
        }
        catch (Exception)
        {
            if (window.IsVisible)
            {
                await CloseDialogSafe(window);
            }
            throw;
        }
    }

    /// <summary>
    /// Avalonia requires an owner for a modal dialog and throws on a null one, naming a parameter
    /// the caller never passed. Failing here names the call they are missing instead.
    /// </summary>
    private Window RequireDialogOwner() =>
        _mainWindow ?? throw new InvalidOperationException(
            "A modal dialog needs an owner window, and no main window has been set. " +
            "Call SetMainWindow before showing a dialog.");

    /// <summary>
    /// Awaits an abandoned dialog so its failure is observed and logged rather than resurfacing
    /// later as an unobserved task exception. The caller is already failing for its own reason.
    /// </summary>
    private async Task ObserveDialogFailure(Task showing)
    {
        try
        {
            await showing;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "The dialog was abandoned because the dialog action failed");
        }
    }

    private static void ShowWindowSafe(Window window)
    {
        if (window.IsVisible)
        {
            window.Activate();
            return;
        }

        WindowFocus.PlaceInitialFocus(window);
        window.Show();
    }

    private (TWindow window, TViewModel viewModel) CreateDialogWindow<TWindow, TViewModel>()
        where TWindow : Window, new()
        where TViewModel : class
    {
        TViewModel viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        TWindow window = new() { DataContext = viewModel };

        return (window, viewModel);
    }

    private Task ConfigureAsModalDialog(Window window) =>
        _dispatcher.InvokeAsync(() => ConfigureModalWindow(window));

    private static void ConfigureModalWindow(Window window) =>
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

    private Task<bool?> ShowDialogInternal(Window window, Window owner) =>
        _dispatcher.InvokeAsync(() => window.ShowDialog<bool?>(owner));

    private Task CloseDialogSafe(Window window) => _dispatcher.InvokeAsync(window.Close);

    public void CloseWindow<TWindow>() where TWindow : Window
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));

        if (!TryGetCachedWindow<TWindow>(out TWindow? window))
            return;

        // Invoke, not InvokeAsync: this method returns void, so posting the close and walking away
        // would let the caller observe an open window and lose any failure.
        _dispatcher.Invoke(window.Close);
    }

    public void CloseAllWindows()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));

        List<Window> windowsToClose = new();
        
        foreach (KeyValuePair<Type, WeakReference<Window>> kvp in _windowCache)
        {
            if (kvp.Value.TryGetTarget(out Window? window) && window != _mainWindow)
            {
                windowsToClose.Add(window);
            }
        }

        _dispatcher.Invoke(() => CloseEach(windowsToClose));
    }

    private static void CloseEach(List<Window> windows)
    {
        foreach (Window window in windows)
        {
            window.Close();
        }
    }

    private bool TryGetCachedWindow<TWindow>([NotNullWhen(true)] out TWindow? window) where TWindow : Window
    {
        window = null;
        
        if (!_windowCache.TryGetValue(typeof(TWindow), out WeakReference<Window>? weakRef))
            return false;

        if (!weakRef.TryGetTarget(out Window? cachedWindow))
        {
            _windowCache.TryRemove(typeof(TWindow), out _);
            return false;
        }

        window = cachedWindow as TWindow;
        return window != null;
    }

    private TWindow CreateWindowWithViewModel<TWindow, TViewModel>()
        where TWindow : Window, new()
        where TViewModel : class
    {
        TViewModel viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        TWindow window = new() { DataContext = viewModel };
        
        CacheWindow(window);
        return window;
    }

    private TWindow CreateWindow<TWindow>() where TWindow : Window, new()
    {
        TWindow window = new();
        CacheWindow(window);
        return window;
    }

    private void CacheWindow<TWindow>(TWindow window) where TWindow : Window
    {
        WeakReference<Window> weakRef = new(window);
        _windowCache.AddOrUpdate(typeof(TWindow), weakRef, (_, _) => weakRef);

        // A closed window must not be handed out again: Show() on it throws. The handler removes
        // itself, so the window stops holding this manager alive the moment it closes - the cache
        // keeps only a weak reference, and this subscription was the one strong link left.
        void OnClosed(object? sender, EventArgs e)
        {
            _windowCache.TryRemove(typeof(TWindow), out _);
            window.Closed -= OnClosed;
        }

        window.Closed += OnClosed;
    }

    private void TryAutoInitializeMainWindow()
    {
        Type? windowType = _options.Window.MainWindowType;
        Type? viewModelType = _options.Window.MainViewModelType;

        if (windowType == null || viewModelType == null)
        {
            windowType ??= _typeRegistry.GetType("MainWindow");
            viewModelType ??= _typeRegistry.GetType("MainWindowViewModel");
            
            if (windowType == null)
            {
                Type[] windowTypes = _typeRegistry.FindTypes("Window").ToArray();
                windowType = windowTypes.FirstOrDefault(t => t.Name.Contains("Main"));
            }
            
            if (viewModelType == null)
            {
                Type[] viewModelTypes = _typeRegistry.GetTypesDerivedFrom<ViewModelBase>().ToArray();
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
                // Auto-discovery is a convenience: the application can still set its main window
                // explicitly, so this warns rather than aborting startup.
                _logger.LogWarning(ex, "Could not auto-initialize the main window from {WindowType}", windowType);
            }
        }
    }

    private void SetMainWindowInternal<TWindow, TViewModel>() 
        where TWindow : Window, new() 
        where TViewModel : class
    {
        TViewModel viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        TWindow window = new() { DataContext = viewModel };
        SetMainWindowInternal(window);
    }

    private void SetMainWindowInternal(Window window)
    {
        _mainWindow = window;
        SetMainWindowForLifetime(_applicationLifetime, _mainWindow);
    }

    private static void SetMainWindowForLifetime(IApplicationLifetime applicationLifetime, Window mainWindow)
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        GC.SuppressFinalize(this);
    }
}