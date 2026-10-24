namespace CdCSharp.Pangea.Dialogs;

/// <summary>
/// The system's pickers for files and folders, answered with paths on this machine.
/// </summary>
/// <remarks>
/// <para>
/// A view model asks for this instead of reaching for a window's <c>StorageProvider</c>: the picker
/// needs a window to own it, which a view model has no business finding, and a screen that opens one
/// is then testable with a double that answers from a script.
/// </para>
/// <para>
/// Every method answers "nothing chosen" - <see langword="null"/>, or an empty list - when the person
/// cancels, when there is no window or view to own the picker yet, and for an item that has no local
/// path, such as a document a mobile platform hands over as content rather than as a file.
/// </para>
/// </remarks>
public interface IFilePickerService
{
    /// <summary>Lets the person choose any number of files.</summary>
    /// <param name="title">The picker's title.</param>
    /// <param name="types">The kinds of file offered, the first one selected. Null offers any file.</param>
    /// <param name="startIn">A folder to start in, or a file whose folder to start in. Null leaves it to the system.</param>
    /// <returns>The files chosen, or an empty list.</returns>
    Task<IReadOnlyList<string>> OpenFilesAsync(string title, IReadOnlyList<FileType>? types = null, string? startIn = null);

    /// <summary>Lets the person choose one file.</summary>
    /// <inheritdoc cref="OpenFilesAsync" path="/param"/>
    /// <returns>The file chosen, or <see langword="null"/>.</returns>
    Task<string?> OpenFileAsync(string title, IReadOnlyList<FileType>? types = null, string? startIn = null);

    /// <summary>Lets the person choose a folder.</summary>
    /// <param name="title">The picker's title.</param>
    /// <param name="startIn">A folder to start in, or a file whose folder to start in. Null leaves it to the system.</param>
    /// <returns>The folder chosen, or <see langword="null"/>.</returns>
    Task<string?> OpenFolderAsync(string title, string? startIn = null);

    /// <summary>Asks where to save a file, offering to replace one that exists.</summary>
    /// <param name="title">The picker's title.</param>
    /// <param name="suggestedName">The name the picker starts with, extension included.</param>
    /// <param name="types">The kinds of file offered; the first one's extension is added when the person types none.</param>
    /// <param name="startIn">A folder to start in, or a file whose folder to start in. Null leaves it to the system.</param>
    /// <returns>Where to write, or <see langword="null"/>. Nothing is written yet.</returns>
    Task<string?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileType>? types = null, string? startIn = null);
}

/// <summary>A kind of file a picker offers: a name and the extensions it covers.</summary>
/// <param name="Name">What the picker shows, such as <c>Images</c>.</param>
/// <param name="Extensions">
/// Written as <c>png</c>, <c>.png</c> or <c>*.png</c>, whichever is at hand; <c>*</c> covers any file.
/// </param>
public sealed record FileType(string Name, IReadOnlyList<string> Extensions)
{
    /// <summary>Any file, under the name the system gives it.</summary>
    public static FileType All { get; } = new("All", ["*"]);

    /// <summary>The extensions without dot or asterisk, lower case, empty ones dropped.</summary>
    public IEnumerable<string> PlainExtensions => Extensions
        .Select(extension => extension.Trim().TrimStart('*').TrimStart('.').ToLowerInvariant())
        .Where(extension => extension.Length > 0);

    /// <summary>Whether this covers any file rather than some.</summary>
    public bool IsAnyFile => Extensions.Any(extension => extension.Trim() is "*" or "*.*");
}
