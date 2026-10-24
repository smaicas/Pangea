using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Windows;

/// <summary>What owns the system's pickers and launcher: the main window, or the single view.</summary>
internal static class MainTopLevel
{
    /// <summary>
    /// The main window on desktop, the single view's top level elsewhere, or <see langword="null"/>
    /// before either exists - and in an application without a lifetime, a headless session or a
    /// designer, where asking the window manager would throw for the lifetime it needs.
    /// </summary>
    public static TopLevel? Find(IServiceProvider services) => Application.Current?.ApplicationLifetime switch
    {
        IClassicDesktopStyleApplicationLifetime desktop => services.GetService<IWindowManager>()?.GetMainWindow() ?? desktop.MainWindow,
        ISingleViewApplicationLifetime { MainView: { } view } => TopLevel.GetTopLevel(view),
        _ => null
    };
}
