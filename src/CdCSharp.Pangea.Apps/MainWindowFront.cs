using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Apps;

/// <summary>Bringing the application's main window in front of whatever the person is looking at.</summary>
internal static class MainWindowFront
{
    /// <summary>
    /// Restores, shows and activates the main window, on the UI thread. Does nothing without a
    /// desktop lifetime - a test, a designer - where there is no window to bring.
    /// </summary>
    public static void Bring(IServiceProvider services)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime) return;
        if (services.GetService<IUIDispatcher>() is not { } dispatcher) return;

        dispatcher.Post(() =>
        {
            if (services.GetService<IWindowManager>()?.GetMainWindow() is not { } window) return;

            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;

            window.Show();
            window.Activate();

            // Activate alone is ignored by Windows when the process is not in the foreground; a
            // moment on top gets the window in front of whatever is.
            window.Topmost = true;
            window.Topmost = false;
        });
    }
}
