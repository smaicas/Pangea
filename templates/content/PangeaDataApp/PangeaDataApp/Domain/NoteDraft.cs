namespace PangeaDataApp.Domain;

/// <summary>
/// What the form holds, tidied into what the table stores.
/// </summary>
/// <remarks>
/// <para>
/// A note about the layer, more than about notes. Everything here is a decision the application
/// makes with no database, no dispatcher and no view in the way, so it can be asked a question
/// directly - see the tests - instead of being reached through a screen.
/// </para>
/// <para>
/// The rule is the useful part: put in <c>Domain</c> what would still be true if the application had
/// a command line instead of a window. What is left in the view model is the wiring, which is the
/// part that needs a running application to mean anything.
/// </para>
/// </remarks>
/// <param name="Title">Trimmed, and never empty by the time this exists.</param>
/// <param name="Body">Trimmed, or null: a body of spaces is a body nobody wrote.</param>
public sealed record NoteDraft(string Title, string? Body)
{
    /// <summary>
    /// Tidies what was typed, or answers null when there is no note in it.
    /// </summary>
    /// <remarks>
    /// Null rather than an exception: an empty box is the ordinary state of a form somebody has not
    /// filled in yet, not a failure. The validation attributes on the view model's fields are what
    /// tell the user about it.
    /// </remarks>
    public static NoteDraft? From(string? title, string? body)
    {
        string tidied = title?.Trim() ?? "";

        if (tidied.Length == 0) return null;

        return new NoteDraft(tidied, string.IsNullOrWhiteSpace(body) ? null : body.Trim());
    }
}
