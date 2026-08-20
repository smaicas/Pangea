using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace CdCSharp.Pangea.Shell;

/// <summary>
/// The root control of an application that has no windows, and everything that would otherwise
/// have been one.
/// </summary>
/// <remarks>
/// <para>
/// On Android, iOS and in the browser the application is given one view and never gets a second.
/// A splash and a modal dialog are both "something in front of the main UI", and with no window to
/// put them in they have to be layered over it instead. This host is that layering: the
/// application's own UI at the bottom, and an overlay layer above it that is empty - and invisible
/// to hit testing - until something is put there.
/// </para>
/// <para>
/// The overlay layer takes pointer input while it is showing, which is what makes a dialog over it
/// modal: the UI underneath cannot be clicked through.
/// </para>
/// </remarks>
internal sealed class PangeaShellHost : Panel
{
    private readonly ContentControl _content = new();
    private readonly Panel _overlays = new()
    {
        IsVisible = false,
        Background = new SolidColorBrush(Colors.Black, 0.45)
    };

    public PangeaShellHost()
    {
        Children.Add(_content);
        Children.Add(_overlays);
    }

    /// <summary>The application's own UI.</summary>
    public Control? MainContent
    {
        get => _content.Content as Control;
        set => _content.Content = value;
    }

    /// <summary>What is layered over it, bottom first.</summary>
    internal IReadOnlyList<Control> Overlays => _overlays.Children.OfType<Control>().ToList();

    public void ShowOverlay(Control overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);

        _overlays.Children.Add(overlay);
        _overlays.IsVisible = true;

        // Nothing behind the overlay can be reached while it is up. Set here rather than in the
        // initialiser because a layer that is not showing must not swallow input either.
        _overlays.IsHitTestVisible = true;

        Windows.WindowFocus.PlaceInitialFocus(overlay);
    }

    public void HideOverlay(Control overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);

        _overlays.Children.Remove(overlay);

        bool empty = _overlays.Children.Count == 0;
        _overlays.IsVisible = !empty;
        _overlays.IsHitTestVisible = !empty;

        // The layer is gone, and so is whatever had focus inside it. Put it back on the main UI
        // rather than leaving the keyboard pointing at nothing.
        if (empty && MainContent is { } main) main.Focus(NavigationMethod.Unspecified);
    }
}
