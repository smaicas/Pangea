using Avalonia.Controls;
using Avalonia.Threading;

namespace CdCSharp.Pangea.Windows;

public static class WindowExtensions
{
    public static Task ShowSafeAsync(this Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (Dispatcher.UIThread.CheckAccess())
        {
            window.Show();
            return Task.CompletedTask;
        }
        
        return Dispatcher.UIThread.InvokeAsync(() => window.Show()).GetTask();
    }
    public static Task CloseSafeAsync(this Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (Dispatcher.UIThread.CheckAccess())
        {
            window.Close();
            return Task.CompletedTask;
        }
        
        return Dispatcher.UIThread.InvokeAsync(() => window.Close()).GetTask();
    }
    public static Task ActivateSafeAsync(this Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (Dispatcher.UIThread.CheckAccess())
        {
            window.Activate();
            return Task.CompletedTask;
        }
        
        return Dispatcher.UIThread.InvokeAsync(() => window.Activate()).GetTask();
    }
    public static Task HideSafeAsync(this Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (Dispatcher.UIThread.CheckAccess())
        {
            window.Hide();
            return Task.CompletedTask;
        }
        
        return Dispatcher.UIThread.InvokeAsync(() => window.Hide()).GetTask();
    }
    public static Task<TResult?> ShowDialogSafeAsync<TResult>(this Window window, Window? parent = null)
    {
        ArgumentNullException.ThrowIfNull(window);
    
        if (Dispatcher.UIThread.CheckAccess())
        {
            return window.ShowDialog<TResult>(parent);
        }
    
        return Dispatcher.UIThread.InvokeAsync(() => window.ShowDialog<TResult>(parent));
    }
    public static void ShowSafe(this Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (Dispatcher.UIThread.CheckAccess())
        {
            window.Show();
        }
        else
        {
            _ = Dispatcher.UIThread.InvokeAsync(() => window.Show());
        }
    }
    public static void CloseSafe(this Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (Dispatcher.UIThread.CheckAccess())
        {
            window.Close();
        }
        else
        {
            _ = Dispatcher.UIThread.InvokeAsync(() => window.Close());
        }
    }
    public static void ActivateSafe(this Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (Dispatcher.UIThread.CheckAccess())
        {
            window.Activate();
        }
        else
        {
            _ = Dispatcher.UIThread.InvokeAsync(() => window.Activate());
        }
    }
    public static void HideSafe(this Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (Dispatcher.UIThread.CheckAccess())
        {
            window.Hide();
        }
        else
        {
            _ = Dispatcher.UIThread.InvokeAsync(() => window.Hide());
        }
    }
}