using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CdCSharp.Pangea.Core.Abstractions;
using System.Reflection;

namespace CdCSharp.Pangea.Startup;

/// <summary>
/// The splash for a platform that has no windows to put one in.
/// </summary>
/// <remarks>
/// Android, iOS and the browser give the application a single view. The splash there is that view
/// until the shell replaces it, so what differs from <see cref="PangeaSplashWindow"/> is the
/// container and the way out of a failure: there is no window chrome to restore and nothing to
/// close, so the report simply stays.
/// </remarks>
internal sealed class PangeaSplashView : UserControl, IPangeaSplashView
{
    private readonly SplashPanel _panel;

    public PangeaSplashView(string? title)
    {
        _panel = new SplashPanel(title ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "Starting")
        {
            MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Painted rather than left transparent: this is the whole screen while it is up, and the
        // resource is observed so it follows a theme variant the application restores underneath it.
        Bind(BackgroundProperty, this.GetResourceObservable("ThemeBackgroundBrush"));

        Content = _panel;
    }

    public void ReportStatus(string status) => _panel.ReportStatus(status);

    /// <summary>
    /// Startup failed, and on this platform the report is where it ends: there is no second window
    /// to fall back to, and closing the only view would leave the user staring at the home screen
    /// with no reason given.
    /// </summary>
    public void ReportFailure(string message) => _panel.ReportFailure(message);
}
