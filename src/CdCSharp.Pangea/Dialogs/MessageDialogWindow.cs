using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace CdCSharp.Pangea.Dialogs;

/// <summary>
/// The window behind <see cref="IDialogService"/>, built in code rather than XAML.
/// </summary>
/// <remarks>
/// Built from plain controls so it takes the application's theme like anything else on screen: the
/// resources it uses are looked up dynamically, so an application that restyles the toolkit
/// restyles this too, without the package shipping a dictionary of its own.
/// </remarks>
internal sealed class MessageDialogWindow : Window
{
    private MessageDialogWindow(string title, string text, string confirmText, string? cancelText)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MinWidth = 320;

        // A modal dialog is not a window of its own in any sense the user cares about: it has one
        // size, it belongs to the window behind it, and minimising it would hide it while it still
        // blocked what it came from. The close button stays - dismissing is a real answer, read as
        // a cancel, and taking it away would leave a dialog with only one way out.
        CanResize = false;
        CanMaximize = false;
        CanMinimize = false;
        ShowInTaskbar = false;

        ConfirmButton = new Button
        {
            Content = confirmText,
            MinWidth = 88,
            IsDefault = true
        };

        ConfirmButton.Click += (_, _) => Close(true);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        if (cancelText is not null)
        {
            CancelButton = new Button
            {
                Content = cancelText,
                MinWidth = 88,
                IsCancel = true
            };

            CancelButton.Click += (_, _) => Close(false);
            buttons.Children.Add(CancelButton);
        }

        buttons.Children.Add(ConfirmButton);

        // The buttons are docked first so they keep their place whatever the message does; the
        // message fills what is left and scrolls inside it. Sized to content but capped: a long
        // message used to grow the window past the screen, and with no scroll and no resize the
        // buttons ended up somewhere unreachable - a dialog nobody could answer.
        MaxHeight = 520;

        buttons.Margin = new Avalonia.Thickness(0, 20, 0, 0);

        ScrollViewer message = new()
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap
            }
        };

        DockPanel content = new()
        {
            Margin = new Avalonia.Thickness(24),
            MaxWidth = 480
        };

        DockPanel.SetDock(buttons, Dock.Bottom);
        content.Children.Add(buttons);
        content.Children.Add(message);

        MessageScroller = message;
        Content = content;

        // Enter and Escape are routed by IsDefault and IsCancel whatever holds focus, but Space acts
        // on the focused control and Tab needs somewhere to start. The default button is named
        // explicitly: left to the general rule it would be Cancel, which is simply the first one in
        // the row.
        Windows.WindowFocus.PlaceInitialFocus(this, () => ConfirmButton);

        Created?.Invoke(this);
    }

    /// <summary>
    /// Raised as each dialog is built, so a test can drive one without a desktop lifetime to
    /// enumerate windows from.
    /// </summary>
    internal static event Action<MessageDialogWindow>? Created;

    /// <summary>Where the message lives, so a test can prove it is the part that scrolls.</summary>
    internal ScrollViewer MessageScroller { get; }

    internal Button ConfirmButton { get; }

    internal Button? CancelButton { get; }

    /// <summary>Closing by the window chrome is the same answer as cancelling.</summary>
    internal static MessageDialogWindow Question(string title, string message, string confirmText, string cancelText) =>
        new(title, message, confirmText, cancelText);

    internal static MessageDialogWindow Statement(string title, string message, string closeText) =>
        new(title, message, closeText, cancelText: null);
}
