using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace CdCSharp.Pangea.Windows;

/// <summary>
/// Gives a window somewhere for the keyboard to start.
/// </summary>
/// <remarks>
/// One rule for every window the toolkit opens, modal or not. What differs between a dialog and an
/// ordinary window is which control deserves focus, not how or when to place it, and having that
/// written twice is how the two quietly drift apart.
/// </remarks>
internal static class WindowFocus
{
    /// <summary>
    /// Focuses <paramref name="preferred"/>, or the first control that can take focus, once the
    /// window is open - and only if the window has not focused something itself.
    /// </summary>
    /// <remarks>
    /// On the window's Opened event rather than now: a control cannot take focus before the
    /// window it lives in exists. Skipped entirely when something already has focus, because a
    /// window that placed focus deliberately knows better than a general rule does.
    /// </remarks>
    internal static void PlaceInitialFocus(Window window, Func<InputElement?>? preferred = null)
    {
        window.Opened += OnOpened;

        void OnOpened(object? sender, EventArgs e)
        {
            window.Opened -= OnOpened;

            if (window.FocusManager?.GetFocusedElement() is not null) return;

            InputElement? target = preferred?.Invoke() ?? FirstFocusable(window);

            target?.Focus();
        }
    }

    private static InputElement? FirstFocusable(Window window) =>
        window.GetVisualDescendants()
            .OfType<InputElement>()
            .FirstOrDefault(candidate =>
                candidate.Focusable && candidate.IsEffectivelyEnabled && candidate.IsEffectivelyVisible);
}
