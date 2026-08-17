using Avalonia.Controls;
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
    private MessageDialogWindow(string title, string message, string confirmText, string? cancelText)
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

        StackPanel content = new()
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 20,
            MaxWidth = 480
        };

        content.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap
        });

        content.Children.Add(buttons);

        Content = content;

        // Something has to hold focus for the keyboard to work at all: Enter and Escape are routed
        // by IsDefault and IsCancel regardless, but Space acts on the focused control and Tab needs
        // somewhere to start. Focusing on Opened rather than here, because a control cannot take
        // focus before the window it lives in exists.
        Opened += (_, _) => ConfirmButton.Focus();

        Created?.Invoke(this);
    }

    /// <summary>
    /// Raised as each dialog is built, so a test can drive one without a desktop lifetime to
    /// enumerate windows from.
    /// </summary>
    internal static event Action<MessageDialogWindow>? Created;

    internal Button ConfirmButton { get; }

    internal Button? CancelButton { get; }

    /// <summary>Closing by the window chrome is the same answer as cancelling.</summary>
    internal static MessageDialogWindow Question(string title, string message, string confirmText, string cancelText) =>
        new(title, message, confirmText, cancelText);

    internal static MessageDialogWindow Statement(string title, string message, string closeText) =>
        new(title, message, closeText, cancelText: null);
}
