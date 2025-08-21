using Avalonia.Controls;
using Avalonia.Threading;
using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace CdCSharp.Pangea.Windows;

public class WindowManagerCore : IWindowManagerCore, IDisposable
{
    private readonly ConcurrentDictionary<Type, WeakReference<Window>> _windowCache = new();
    private readonly SemaphoreSlim _creationSemaphore = new(1, 1);
    private readonly IServiceProvider _serviceProvider;
    private volatile bool _disposed;

    public WindowManagerCore(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TWindow> ShowWindowAsync<TWindow, TViewModel>(NavigationParameter? navigationParameter = null)
        where TWindow : Window, new()
        where TViewModel : class
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManagerCore));

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
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManagerCore));
        
        TViewModel viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        TWindow window = new();
        window.DataContext = viewModel;
        return window;
    }

    public TWindow CreateWindow<TWindow>() where TWindow : Window, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManagerCore));
        return new TWindow();
    }

    public void CloseWindow<TWindow>() where TWindow : Window
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManagerCore));

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
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManagerCore));
        return TryGetCachedWindow<TWindow>(out _);
    }

    public TWindow? GetWindow<TWindow>() where TWindow : Window
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(WindowManagerCore));
        TryGetCachedWindow<TWindow>(out TWindow? window);
        return window;
    }

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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (WeakReference<Window> weakRef in _windowCache.Values.ToList())
            if (weakRef.TryGetTarget(out Window? window))
                window.Close();

        _windowCache.Clear();
        _creationSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}