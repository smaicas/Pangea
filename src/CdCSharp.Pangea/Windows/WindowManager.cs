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


public class WindowManager : IWindowManager, IDisposable
{
    private readonly IWindowManagerCore _core;
    private readonly IMainWindowManager _mainWindowManager;
    private bool _disposed;

    public WindowManager(IWindowManagerCore core, IMainWindowManager mainWindowManager)
    {
        _core = core;
        _mainWindowManager = mainWindowManager;
    }

    // IWindowManagerCore delegation
    public Task<TWindow> ShowWindowAsync<TWindow, TViewModel>(NavigationParameter? navigationParameter = null)
        where TWindow : Window, new() where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        return _core.ShowWindowAsync<TWindow, TViewModel>(navigationParameter);
    }

    public TWindow CreateWindow<TWindow, TViewModel>() where TWindow : Window, new() where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        return _core.CreateWindow<TWindow, TViewModel>();
    }

    public TWindow CreateWindow<TWindow>() where TWindow : Window, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        return _core.CreateWindow<TWindow>();
    }

    public void CloseWindow<TWindow>() where TWindow : Window
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        _core.CloseWindow<TWindow>();
    }

    public bool IsWindowOpen<TWindow>() where TWindow : Window
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        return _core.IsWindowOpen<TWindow>();
    }

    public TWindow? GetWindow<TWindow>() where TWindow : Window
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        return _core.GetWindow<TWindow>();
    }

    // IMainWindowManager delegation
    public Window? GetMainWindow()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        return _mainWindowManager.GetMainWindow();
    }

    public void SetMainWindow<TWindow, TViewModel>() where TWindow : Window, new() where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        _mainWindowManager.SetMainWindow<TWindow, TViewModel>();
    }

    public void SetMainWindow(Window window)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManager));
        _mainWindowManager.SetMainWindow(window);
    }

    // IDisposable implementation
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Dispose the core component if it implements IDisposable
        if (_core is IDisposable disposableCore)
            disposableCore.Dispose();

        // MainWindowManager doesn't need disposal as it doesn't hold disposable resources
        
        GC.SuppressFinalize(this);
    }
}