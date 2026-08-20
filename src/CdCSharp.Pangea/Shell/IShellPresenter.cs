using Avalonia.Controls;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Dialogs;

namespace CdCSharp.Pangea.Shell;

/// <summary>
/// Where the application's UI goes on the platform it is running on.
/// </summary>
/// <remarks>
/// <para>
/// A desktop application has windows: the shell is one, the splash is another, and a dialog is a
/// third that owns the input while it is up. Android, iOS and the browser hand the application a
/// single view and never a second one - a <see cref="Window"/> cannot even be constructed there,
/// because no windowing platform is registered to build it with.
/// </para>
/// <para>
/// Everything that would otherwise say "open a window" goes through here instead, so the difference
/// is decided once at startup rather than guessed at every call site.
/// </para>
/// </remarks>
public interface IShellPresenter
{
    /// <summary>True when the platform shows one view and has no windows to open.</summary>
    bool IsSingleView { get; }

    /// <summary>Puts the application's main UI on screen.</summary>
    void ShowMain();

    /// <summary>
    /// Puts a splash on screen while startup runs, or returns <see langword="null"/> when the
    /// application asked for none.
    /// </summary>
    /// <returns>
    /// What was shown - a window on desktop, a view on a single-view platform. It implements
    /// <see cref="Core.Abstractions.IPangeaSplashView"/> when it can report what startup is doing.
    /// </returns>
    Control? ShowSplash(PangeaStartupOptions options);

    /// <summary>Takes the splash back off, once the main UI has replaced it.</summary>
    void HideSplash(Control? splash);

    /// <summary>Asks the user a question and waits for the answer.</summary>
    /// <remarks>Called on the UI thread.</remarks>
    Task<bool> ShowMessageAsync(MessageDialogRequest request);
}

/// <summary>A message to put in front of the user, and what the buttons on it say.</summary>
/// <param name="Title">The heading.</param>
/// <param name="Message">The body.</param>
/// <param name="ConfirmText">The button that answers <see langword="true"/>.</param>
/// <param name="CancelText">
/// The button that answers <see langword="false"/>, or <see langword="null"/> for a statement with
/// nothing to cancel.
/// </param>
public sealed record MessageDialogRequest(string Title, string Message, string ConfirmText, string? CancelText);
