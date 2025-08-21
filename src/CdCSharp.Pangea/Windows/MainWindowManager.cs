using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Windows;

public class MainWindowManager : IMainWindowManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IApplicationLifetime _applicationLifetime;
    private readonly PangeaOptions _options;
    private Window? _mainWindow;
    private bool _initialized;

    public MainWindowManager(
        IServiceProvider serviceProvider,
        IApplicationLifetime applicationLifetime,
        IOptions<PangeaOptions> options)
    {
        _serviceProvider = serviceProvider;
        _applicationLifetime = applicationLifetime;
        _options = options.Value;
        
        if (_options.Window.AutoDiscoverMainWindow)
        {
            TryAutoInitializeMainWindow();
        }
    }

    public Window? GetMainWindow() => _mainWindow;

    public void SetMainWindow<TWindow, TViewModel>() 
        where TWindow : Window, new() 
        where TViewModel : class
    {
        TViewModel viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        TWindow window = new();
        window.DataContext = viewModel;
        
        SetMainWindowInternal(window);
    }

    public void SetMainWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        SetMainWindowInternal(window);
    }

    private void TryAutoInitializeMainWindow()
    {
        if (_initialized) return;
        _initialized = true;
        
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
                // Usamos Scoped para evitar referencia circular
                using IServiceScope scope = _serviceProvider.CreateScope();
                object viewModel = scope.ServiceProvider.GetRequiredService(viewModelType);
                
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

    private void SetMainWindowInternal(Window window)
    {
        _mainWindow = window;
        SetMainWindowForLifetime(_applicationLifetime, _mainWindow);
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