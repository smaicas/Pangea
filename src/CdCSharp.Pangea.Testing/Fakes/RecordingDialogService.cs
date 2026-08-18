using CdCSharp.Pangea.Dialogs;

namespace CdCSharp.Pangea.Testing.Fakes;

/// <summary>What was asked, and how it was worded.</summary>
public sealed record DialogRequest(string Title, string Message, string ConfirmText, string CancelText);

/// <summary>
/// Answers dialogs from a script instead of showing them, and remembers what was asked.
/// </summary>
/// <remarks>
/// A view model that confirms before acting cannot be tested against the real dialog service: it
/// needs a window to own the dialog and a user to click it. This answers immediately, so a test can
/// say what the user chose and then assert on what happened - and on the question itself, which is
/// usually a localized string worth checking.
/// </remarks>
public sealed class RecordingDialogService : IDialogService
{
    private readonly Queue<bool> _scripted = new();

    /// <summary>The answer given once the script runs out. <see langword="false"/> by default.</summary>
    public bool DefaultAnswer { get; set; }

    /// <summary>Every confirmation asked, in order.</summary>
    public List<DialogRequest> Confirmations { get; } = [];

    /// <summary>Every statement shown, in order.</summary>
    public List<DialogRequest> Alerts { get; } = [];

    /// <summary>Queues the answers the user will give, one per confirmation, in order.</summary>
    public RecordingDialogService Answering(params bool[] answers)
    {
        foreach (bool answer in answers) _scripted.Enqueue(answer);

        return this;
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel")
    {
        Confirmations.Add(new DialogRequest(title, message, confirmText, cancelText));

        return Task.FromResult(_scripted.Count > 0 ? _scripted.Dequeue() : DefaultAnswer);
    }

    public Task AlertAsync(string title, string message, string closeText = "OK")
    {
        Alerts.Add(new DialogRequest(title, message, closeText, closeText));

        return Task.CompletedTask;
    }
}
