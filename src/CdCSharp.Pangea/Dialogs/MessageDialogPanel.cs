using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace CdCSharp.Pangea.Dialogs;

/// <summary>
/// What a message dialog shows, without deciding what it is shown in.
/// </summary>
/// <remarks>
/// A desktop application puts this in a modal window; one with no windows to open - Android, iOS,
/// the browser - layers it over the shell instead. The message, the buttons and what each one
/// answers are the same either way.
/// </remarks>
internal sealed class MessageDialogPanel : DockPanel
{
    /// <param name="text">The message.</param>
    /// <param name="confirmText">What the confirm button says.</param>
    /// <param name="cancelText">
    /// What the cancel button says, or <see langword="null"/> for a statement with nothing to
    /// cancel.
    /// </param>
    /// <param name="answer">
    /// What the chosen button means. Called once, with <see langword="true"/> for the confirm
    /// button and <see langword="false"/> for the cancel one.
    /// </param>
    internal MessageDialogPanel(string text, string confirmText, string? cancelText, Action<bool> answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        Margin = new Avalonia.Thickness(24);
        MaxWidth = 480;

        ConfirmButton = new Button
        {
            Content = confirmText,
            MinWidth = 88,
            IsDefault = true
        };

        ConfirmButton.Click += (_, _) => answer(true);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 20, 0, 0)
        };

        if (cancelText is not null)
        {
            CancelButton = new Button
            {
                Content = cancelText,
                MinWidth = 88,
                IsCancel = true
            };

            CancelButton.Click += (_, _) => answer(false);
            buttons.Children.Add(CancelButton);
        }

        buttons.Children.Add(ConfirmButton);

        // The buttons are docked first so they keep their place whatever the message does; the
        // message fills what is left and scrolls inside it. A long message used to grow the dialog
        // past the screen, and with no scroll the buttons ended up somewhere unreachable - a dialog
        // nobody could answer.
        MessageScroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap
            }
        };

        SetDock(buttons, Dock.Bottom);
        Children.Add(buttons);
        Children.Add(MessageScroller);
    }

    /// <summary>Where the message lives, so a test can prove it is the part that scrolls.</summary>
    internal ScrollViewer MessageScroller { get; }

    internal Button ConfirmButton { get; }

    internal Button? CancelButton { get; }
}
