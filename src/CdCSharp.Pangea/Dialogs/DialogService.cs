using Avalonia.Controls;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Windows;

namespace CdCSharp.Pangea.Dialogs;

/// <inheritdoc cref="IDialogService"/>
public class DialogService : IDialogService
{
    private readonly IWindowManager _windowManager;
    private readonly IUIDispatcher _dispatcher;

    public DialogService(IWindowManager windowManager, IUIDispatcher dispatcher)
    {
        _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK",
        string cancelText = "Cancel")
    {
        ArgumentNullException.ThrowIfNull(message);

        return ShowAsync(() => MessageDialogWindow.Question(title, message, confirmText, cancelText));
    }

    public async Task AlertAsync(string title, string message, string closeText = "OK")
    {
        ArgumentNullException.ThrowIfNull(message);

        await ShowAsync(() => MessageDialogWindow.Statement(title, message, closeText));
    }

    /// <summary>
    /// Builds and shows the dialog on the UI thread, and answers what the user chose.
    /// </summary>
    /// <remarks>
    /// Closing a dialog by its window chrome produces no result at all, which is the same intent as
    /// cancelling and is read as such.
    /// </remarks>
    private Task<bool> ShowAsync(Func<MessageDialogWindow> build) =>
        _dispatcher.InvokeAsync(async () =>
        {
            Window owner = _windowManager.GetMainWindow()
                ?? throw new InvalidOperationException(
                    "A dialog needs an owner window, and no main window has been set. " +
                    "Call SetMainWindow before showing a dialog.");

            MessageDialogWindow dialog = build();

            return await dialog.ShowDialog<bool?>(owner) ?? false;
        });
}
