using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Dialogs;

/// <inheritdoc cref="IDialogService"/>
/// <remarks>
/// What a dialog actually is belongs to the platform: a modal window owned by the shell on desktop,
/// and a card layered over it where there are no windows to open. This asks the question and leaves
/// that to <see cref="IShellPresenter"/>.
/// </remarks>
public class DialogService : IDialogService
{
    private readonly Func<IShellPresenter> _shell;
    private readonly IUIDispatcher _dispatcher;

    /// <summary>
    /// The constructor the container uses.
    /// </summary>
    /// <remarks>
    /// The shell is looked up when a dialog is actually shown, not when this is built. Deciding
    /// which shell the application has needs the lifetime, and an application hosted without one -
    /// a headless test session, a XAML designer - still builds every view model it has, including
    /// the ones that would ask a question if they ever ran.
    /// </remarks>
    public DialogService(IServiceProvider serviceProvider, IUIDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _shell = serviceProvider.GetRequiredService<IShellPresenter>;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    private DialogService(Func<IShellPresenter> shell, IUIDispatcher dispatcher)
    {
        _shell = shell;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// The shell named outright, for a caller building this by hand.
    /// </summary>
    /// <remarks>
    /// A factory rather than a second constructor: two constructors of the same length leave the
    /// container unable to choose between them, and it says so at the first resolution rather than
    /// here.
    /// </remarks>
    public static DialogService For(IShellPresenter shell, IUIDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(shell);

        return new DialogService(() => shell, dispatcher);
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK",
        string cancelText = "Cancel")
    {
        ArgumentNullException.ThrowIfNull(message);

        return ShowAsync(new MessageDialogRequest(title, message, confirmText, cancelText));
    }

    public async Task AlertAsync(string title, string message, string closeText = "OK")
    {
        ArgumentNullException.ThrowIfNull(message);

        await ShowAsync(new MessageDialogRequest(title, message, closeText, CancelText: null));
    }

    /// <summary>
    /// Shows the dialog on the UI thread, and answers what the user chose.
    /// </summary>
    /// <remarks>
    /// Dismissing the dialog without choosing produces no result at all, which is the same intent
    /// as cancelling and is read as such.
    /// </remarks>
    private Task<bool> ShowAsync(MessageDialogRequest request) =>
        _dispatcher.InvokeAsync(() => _shell().ShowMessageAsync(request));
}
