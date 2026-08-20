using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace CdCSharp.Pangea.Dialogs;

/// <summary>
/// The message dialog for a platform with no windows to open one in.
/// </summary>
/// <remarks>
/// Android, iOS and the browser give the application a single view, so a modal question is a card
/// layered over the shell rather than a window in front of it. What makes it modal is the overlay
/// it sits in, which takes the pointer input the UI underneath would otherwise get.
/// <para>
/// Escape answers it as a cancel, matching what dismissing the window does on desktop. There is no
/// window chrome here, so that is the only way to dismiss without choosing.
/// </para>
/// </remarks>
internal sealed class MessageDialogView : UserControl
{
    private readonly TaskCompletionSource<bool> _answered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly MessageDialogPanel _panel;

    private MessageDialogView(string title, string text, string confirmText, string? cancelText)
    {
        _panel = new MessageDialogPanel(text, confirmText, cancelText, Answer);

        StackPanel card = new()
        {
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(24, 24, 24, 0)
                },
                _panel
            }
        };

        Border surface = new()
        {
            CornerRadius = new CornerRadius(12),
            MinWidth = 280,
            MaxWidth = 480,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = card
        };

        // Observed rather than read once, so a dialog that is up when the theme variant changes
        // follows it like everything else on screen.
        surface.Bind(Border.BackgroundProperty, this.GetResourceObservable("ThemeBackgroundBrush"));
        surface.Bind(Border.BorderBrushProperty, this.GetResourceObservable("ThemeBorderMidBrush"));
        surface.BorderThickness = new Thickness(1);

        Content = surface;

        Windows.WindowFocus.PlaceInitialFocus(this, () => _panel.ConfirmButton);
    }

    /// <summary>
    /// Raised as each dialog is built, for the reason the window's event is: a test needs the
    /// dialog it is about to answer, and there is no window list to find it in.
    /// </summary>
    internal static event Action<MessageDialogView>? Created;

    internal Task<bool> Answered => _answered.Task;

    internal Button ConfirmButton => _panel.ConfirmButton;

    internal Button? CancelButton => _panel.CancelButton;

    internal static MessageDialogView Question(string title, string message, string confirmText, string cancelText) =>
        Announce(new MessageDialogView(title, message, confirmText, cancelText));

    internal static MessageDialogView Statement(string title, string message, string closeText) =>
        Announce(new MessageDialogView(title, message, closeText, cancelText: null));

    /// <summary>Dismissing without choosing is a cancel, the same as closing the window is.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Escape)
        {
            e.Handled = true;
            Answer(false);
            return;
        }

        base.OnKeyDown(e);
    }

    private void Answer(bool result) => _answered.TrySetResult(result);

    private static MessageDialogView Announce(MessageDialogView dialog)
    {
        Created?.Invoke(dialog);
        return dialog;
    }
}
