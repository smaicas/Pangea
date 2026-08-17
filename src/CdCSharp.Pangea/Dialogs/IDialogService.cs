namespace CdCSharp.Pangea.Dialogs;

/// <summary>
/// Asks the user something, without an application having to write a window for it.
/// </summary>
/// <remarks>
/// Deliberately two questions and nothing else. A dialog with its own layout, fields or result type
/// is a view model and a window like any other, and
/// <see cref="Windows.IWindowManager.ShowDialogAsync{TWindow, TViewModel, TResult}"/> already shows
/// one; growing this interface towards that would only produce a worse way of doing it.
/// </remarks>
public interface IDialogService
{
    /// <summary>Asks a yes-or-no question and waits for the answer.</summary>
    /// <returns><see langword="true"/> when the user confirmed.</returns>
    /// <exception cref="InvalidOperationException">No main window has been set to own the dialog.</exception>
    Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel");

    /// <summary>Tells the user something and waits for them to acknowledge it.</summary>
    /// <exception cref="InvalidOperationException">No main window has been set to own the dialog.</exception>
    Task AlertAsync(string title, string message, string closeText = "OK");
}
