using Avalonia.Controls;
using Avalonia.Media;
using CdCSharp.Pangea.Core.Abstractions;

namespace CdCSharp.Pangea.Startup;

/// <summary>
/// What the built-in splash actually shows, without deciding what it is shown in.
/// </summary>
/// <remarks>
/// A desktop application puts this in a window and a single-view one layers it over the shell.
/// The visual and the two reports are the same either way, and having them written twice is how
/// the two quietly drift apart - so the container differs and this does not.
/// </remarks>
internal sealed class SplashPanel : StackPanel, IPangeaSplashView
{
    private readonly TextBlock _status;
    private readonly ProgressBar _progress;

    internal SplashPanel(string title)
    {
        Margin = new Avalonia.Thickness(28, 24, 28, 24);

        _status = new TextBlock
        {
            Text = string.Empty,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75
        };

        _progress = new ProgressBar
        {
            IsIndeterminate = true,
            Height = 4,
            Margin = new Avalonia.Thickness(0, 16, 0, 12)
        };

        Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });

        Children.Add(_progress);
        Children.Add(_status);
    }

    public void ReportStatus(string status) => _status.Text = status;

    /// <summary>
    /// Turns the splash into the failure report: the progress that is no longer running goes, and
    /// the message takes the place of the status it was showing.
    /// </summary>
    public void ReportFailure(string message)
    {
        _progress.IsIndeterminate = false;
        _progress.IsVisible = false;
        _status.Opacity = 1;
        _status.Text = message;
    }

    /// <summary>Adds a way out, once and only once.</summary>
    internal void AddDismissButton(Button dismiss)
    {
        if (Children.OfType<Button>().Any()) return;

        Children.Add(dismiss);
    }
}
