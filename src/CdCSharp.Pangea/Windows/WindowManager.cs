using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

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
        Task<TResult> ShowDialogAsync<TWindow, TViewModel, TResult>(
        Func<TViewModel, Task<TResult>> dialogAction)
        where TWindow : Window, new() 
        where TViewModel : class;
        
    Task<bool?> ShowDialogAsync<TWindow, TViewModel>()
        where TWindow : Window, new() 
        where TViewModel : class;
}

public class WindowManager : IWindowManager, IDisposable
{
    private readonly ConcurrentDictionary<Type, WeakReference<Window>> _windowCache = new();
    private readonly SemaphoreSlim _creationSemaphore = new(1, 1);
    private readonly IServiceProvider _serviceProvider;
    private readonly IApplicationLifetime _applicationLifetime;
    private readonly PangeaOptions _options;
    private Window? _mainWindow;
    private bool _initialized;
    private volatile bool _disposed;

    public WindowManager(
        IServiceProvider serviceProvider,
        IApplicationLifetime applicationLifetime,
        IOptions<PangeaOptions> options)
    {
        _serviceProvider = serviceProvider;
        _applicationLifetime = applicationLifetime;
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
        
        if (Dispatcher.UIThread.CheckAccess())
        {
            SetMainWindowInternal<TWindow, TViewModel>();
        }
        else
        {
            Dispatcher.UIThread.Invoke(() => SetMainWindowInternal<TWindow, TViewModel>());
        }
    }

    public void SetMainWindow(Window window)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        ArgumentNullException.ThrowIfNull(window);
        
        if (Dispatcher.UIThread.CheckAccess())
        {
            SetMainWindowInternal(window);
        }
        else
        {
            Dispatcher.UIThread.Invoke(() => SetMainWindowInternal(window));
        }
    }

    public TWindow GetOrCreateWindow<TWindow, TViewModel>()
        where TWindow : Window, new()
        where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        
        if (TryGetCachedWindow<TWindow>(out TWindow? existingWindow))
            return existingWindow;

        if (Dispatcher.UIThread.CheckAccess())
        {
            return CreateWindowWithViewModel<TWindow, TViewModel>();
        }
        else
        {
            return Dispatcher.UIThread.Invoke(() => CreateWindowWithViewModel<TWindow, TViewModel>());
        }
    }

    public TWindow GetOrCreateWindow<TWindow>() where TWindow : Window, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));

        if (TryGetCachedWindow<TWindow>(out TWindow? existingWindow))
            return existingWindow;

        if (Dispatcher.UIThread.CheckAccess())
        {
            return CreateWindow<TWindow>();
        }
        else
        {
            return Dispatcher.UIThread.Invoke(() => CreateWindow<TWindow>());
        }
    }

    public async Task<TWindow> ShowWindowAsync<TWindow, TViewModel>()
        where TWindow : Window, new()
        where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));

        // Obtener o crear la ventana primero (thread-safe)
        TWindow window = GetOrCreateWindow<TWindow, TViewModel>();
        
        // Mostrar ventana usando Dispatcher thread-safe
        if (Dispatcher.UIThread.CheckAccess())
        {
            ShowWindowSafe(window);
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => ShowWindowSafe(window));
        }

        return window;
    }

    public async Task<TResult> ShowDialogAsync<TWindow, TViewModel, TResult>(
        Func<TViewModel, Task<TResult>> dialogAction)
        where TWindow : Window, new() 
        where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        ArgumentNullException.ThrowIfNull(dialogAction);

        // Crear una nueva instancia para diálogos (no usar caché)
        TViewModel viewModel;
        TWindow window;
        
        if (Dispatcher.UIThread.CheckAccess())
        {
            (window, viewModel) = CreateDialogWindow<TWindow, TViewModel>();
        }
        else
        {
            (window, viewModel) = await Dispatcher.UIThread.InvokeAsync(() => 
                CreateDialogWindow<TWindow, TViewModel>());
        }

        try
        {
            // Configurar como modal
            await ConfigureAsModalDialog(window);

            // Ejecutar la acción del diálogo en paralelo con el ShowDialog
            Task<TResult> dialogTask = dialogAction(viewModel);
            
            // Mostrar el diálogo modal
            Task<bool?> showDialogTask = ShowDialogInternal(window);

            // Esperar a que termine la acción del diálogo
            TResult result = await dialogTask;

            // Cerrar el diálogo si aún está abierto
            if (window.IsVisible)
            {
                await CloseDialogSafe(window);
            }

            return result;
        }
        catch (Exception)
        {
            // Asegurar que el diálogo se cierre en caso de error
            if (window.IsVisible)
            {
                await CloseDialogSafe(window);
            }
            throw;
        }
    }

    public async Task<bool?> ShowDialogAsync<TWindow, TViewModel>()
        where TWindow : Window, new() 
        where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));

        // Crear una nueva instancia para diálogos (no usar caché)
        TWindow window;
        
        if (Dispatcher.UIThread.CheckAccess())
        {
            (window, _) = CreateDialogWindow<TWindow, TViewModel>();
        }
        else
        {
            (window, _) = await Dispatcher.UIThread.InvokeAsync(() => 
                CreateDialogWindow<TWindow, TViewModel>());
        }

        try
        {
            // Configurar como modal
            await ConfigureAsModalDialog(window);

            // Mostrar el diálogo modal y devolver el resultado
            return await ShowDialogInternal(window);
        }
        catch (Exception)
        {
            // Asegurar que el diálogo se cierre en caso de error
            if (window.IsVisible)
            {
                await CloseDialogSafe(window);
            }
            throw;
        }
    }

    private static void ShowWindowSafe(Window window)
    {
        if (window.IsVisible)
        {
            window.Activate();
        }
        else
        {
            window.Show();
        }
    }

    private (TWindow window, TViewModel viewModel) CreateDialogWindow<TWindow, TViewModel>()
        where TWindow : Window, new()
        where TViewModel : class
    {
        TViewModel viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        TWindow window = new() { DataContext = viewModel };
        
        // No cachear ventanas de diálogo ya que son de un solo uso
        return (window, viewModel);
    }

    private async Task ConfigureAsModalDialog(Window window)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ConfigureModalWindow(window);
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => ConfigureModalWindow(window));
        }
    }

    private void ConfigureModalWindow(Window window)
    {
        // Configurar propiedades del diálogo modal
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        
        // Establecer la ventana padre si existe una ventana principal
        if (_mainWindow != null && _mainWindow.IsVisible)
        {
            // Note: En Avalonia, ShowDialog automáticamente maneja el owner
            // si se llama desde una ventana padre
        }
    }

    private async Task<bool?> ShowDialogInternal(Window window)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return await window.ShowDialog<bool?>(_mainWindow);
        }
        else
        {
            return await Dispatcher.UIThread.InvokeAsync(async () => 
                await window.ShowDialog<bool?>(_mainWindow));
        }
    }

    private async Task CloseDialogSafe(Window window)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            window.Close();
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => window.Close());
        }
    }

    public void CloseWindow<TWindow>() where TWindow : Window
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));

        if (!TryGetCachedWindow<TWindow>(out TWindow? window))
            return;

        if (Dispatcher.UIThread.CheckAccess())
        {
            window.Close();
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(() => window.Close());
        }
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

        if (Dispatcher.UIThread.CheckAccess())
        {
            foreach (Window window in windowsToClose)
            {
                window.Close();
            }
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (Window window in windowsToClose)
                {
                    window.Close();
                }
            });
        }
    }

    private bool TryGetCachedWindow<TWindow>(out TWindow? window) where TWindow : Window
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
        
        // Remover del cache cuando se cierre la ventana
        window.Closed += (_, _) => _windowCache.TryRemove(typeof(TWindow), out _);
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

        try
        {
            _creationSemaphore.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }
        
        GC.SuppressFinalize(this);
    }
}