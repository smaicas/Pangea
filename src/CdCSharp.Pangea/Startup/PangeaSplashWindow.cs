using Avalonia.Controls;
using Avalonia.Layout;
using CdCSharp.Pangea.Core.Abstractions;
using System.Reflection;

namespace CdCSharp.Pangea.Startup;

/// <summary>
/// What stands in for the main window while the startup initializers run.
/// </summary>
/// <remarks>
/// Built from plain controls rather than XAML, like <see cref="Dialogs.MessageDialogWindow"/>, so
/// it takes the application's theme without the package shipping a dictionary of its own. It is
/// deliberately plain: an application that wants a logo points
/// <see cref="Core.Configuration.PangeaStartupOptions.SplashWindowType"/> at its own window.
/// <para>
/// The visual itself lives in <see cref="SplashPanel"/>, which a single-view application layers
/// over its shell instead - there being no window to put it in on Android or iOS.
/// </para>
/// </remarks>
internal sealed class PangeaSplashWindow : Window, IPangeaSplashView
{
    private readonly SplashPanel _panel;

    public PangeaSplashWindow(string? title)
    {
        Title = title ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "Starting";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        CanResize = false;
        CanMaximize = false;
        CanMinimize = false;
        ShowInTaskbar = true;

        // No system decorations: this is not a window the user does anything with, and a close
        // button on it would offer to cancel work that has no cancellation to offer.
        WindowDecorations = WindowDecorations.BorderOnly;

        _panel = new SplashPanel(Title);

        Content = _panel;
    }

    public void ReportStatus(string status) => _panel.ReportStatus(status);

    /// <summary>
    /// Turns the splash into the failure report. Nothing else is on screen at this point, so the
    /// window that was going to be replaced becomes the only place the reason can be read.
    /// </summary>
    public void ReportFailure(string message)
    {
        _panel.ReportFailure(message);

        // Given up on, so it is now an ordinary window: it can be closed, and closing it ends a
        // desktop application that has nothing else open.
        WindowDecorations = WindowDecorations.Full;
        CanResize = true;

        Button close = new()
        {
            Content = "Close",
            MinWidth = 88,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 20, 0, 0),
            IsDefault = true,
            IsCancel = true
        };

        close.Click += (_, _) => Close();

        _panel.AddDismissButton(close);
    }
}
