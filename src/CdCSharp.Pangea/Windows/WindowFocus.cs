using Avalonia;
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

    /// <summary>
    /// The same rule for a control that is not a window: what a single-view application layers over
    /// its main view instead of opening a window for it.
    /// </summary>
    /// <remarks>
    /// On <see cref="Visual.AttachedToVisualTree"/> for the reason the window overload waits for
    /// <c>Opened</c>: a control cannot take focus before it is in a tree that is on screen. An
    /// overlay that is already attached raises nothing, so that case is handled rather than waited
    /// for. Focus already inside <paramref name="root"/> is left alone - the overlay placed it
    /// itself, and it knows better than a general rule does.
    /// </remarks>
    internal static void PlaceInitialFocus(Control root, Func<InputElement?>? preferred = null)
    {
        if (root.IsAttachedToVisualTree())
        {
            Place();
            return;
        }

        root.AttachedToVisualTree += OnAttached;

        void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            root.AttachedToVisualTree -= OnAttached;
            Place();
        }

        void Place()
        {
            if (TopLevel.GetTopLevel(root)?.FocusManager?.GetFocusedElement() is Visual focused &&
                IsWithin(focused, root))
            {
                return;
            }

            InputElement? target = preferred?.Invoke() ?? FirstFocusable(root);

            target?.Focus();
        }
    }

    private static bool IsWithin(Visual candidate, Visual root) =>
        ReferenceEquals(candidate, root) ||
        candidate.GetVisualAncestors().Any(ancestor => ReferenceEquals(ancestor, root));

    private static InputElement? FirstFocusable(Visual root) =>
        root.GetVisualDescendants()
            .OfType<InputElement>()
            .FirstOrDefault(candidate =>
                candidate.Focusable && candidate.IsEffectivelyEnabled && candidate.IsEffectivelyVisible);
}
