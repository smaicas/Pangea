using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CdCSharp.Pangea.Windows;

/// <summary>
/// Opt-in keyboard behaviour for a window.
/// </summary>
public static class WindowBehavior
{
    /// <summary>
    /// Closes the window when Escape is pressed.
    /// </summary>
    /// <remarks>
    /// Off by default, and deliberately so. Escape dismissing a modal dialog is a convention
    /// everywhere; Escape closing an ordinary window is not, and on a window holding unsaved work it
    /// destroys it with one keystroke. Alt+F4 - or the platform's equivalent - is what closes a
    /// window. This exists for the case in between: a secondary window a person opened to look at
    /// something, where reaching for Alt+F4 feels absurd.
    /// </remarks>
    public static readonly AttachedProperty<bool> CloseOnEscapeProperty =
        AvaloniaProperty.RegisterAttached<Window, bool>("CloseOnEscape", typeof(WindowBehavior));

    static WindowBehavior() => CloseOnEscapeProperty.Changed.AddClassHandler<Window>(OnCloseOnEscapeChanged);

    public static void SetCloseOnEscape(Window window, bool value) =>
        window.SetValue(CloseOnEscapeProperty, value);

    public static bool GetCloseOnEscape(Window window) => window.GetValue(CloseOnEscapeProperty);

    private static void OnCloseOnEscapeChanged(Window window, AvaloniaPropertyChangedEventArgs args)
    {
        window.KeyDown -= OnKeyDown;

        if (args.GetNewValue<bool>())
        {
            // Tunnelling, so a focused control that would swallow Escape does not hide it.
            window.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        }
        else
        {
            window.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        }
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || sender is not Window window) return;

        e.Handled = true;
        window.Close();
    }
}
