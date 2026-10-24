namespace CdCSharp.Pangea.Files;

/// <summary>
/// What a file list says, on its panel and about each file. English unless the application gives its own.
/// </summary>
/// <remarks>
/// A record, so an application states only what it changes:
/// <code>
/// FileList images = new(IsImage) { Texts = FileListTexts.English with { AddFiles = "Añadir imágenes", Done = "Hecho" } };
/// </code>
/// </remarks>
public sealed record FileListTexts
{
    public static FileListTexts English { get; } = new();

    public string AddFiles { get; init; } = "Add files";

    public string AddFolder { get; init; } = "Folder";

    /// <summary>The title of the picker the add button opens.</summary>
    public string AddFilesTitle { get; init; } = "Add files";

    public string AddFolderTitle { get; init; } = "Add a folder, with its subfolders";

    public string Sort { get; init; } = "Sort";

    public string SortTip { get; init; } = "By name";

    public string Clear { get; init; } = "Clear";

    /// <summary>What the empty list says; a panel's own <c>EmptyText</c> replaces it.</summary>
    public string Empty { get; init; } = "Drop files or folders here";

    public string OneFile { get; init; } = "1 file";

    /// <summary>A composite format: <c>{0}</c> is the count.</summary>
    public string ManyFiles { get; init; } = "{0} files";

    public string ShowResult { get; init; } = "Show the result";

    public string MoveUp { get; init; } = "Move up";

    public string MoveDown { get; init; } = "Move down";

    public string Remove { get; init; } = "Remove from the list";

    public string Working { get; init; } = "Working…";

    /// <summary>A file done whose result says nothing more.</summary>
    public string Done { get; init; } = "Done";

    /// <summary>A file left alone, because its result was already there.</summary>
    public string Skipped { get; init; } = "Already there";

    public string Failed { get; init; } = "Error";

    public string Canceled { get; init; } = "Canceled";

    /// <summary>"1 file", "3 files", or nothing for none.</summary>
    public string Count(int count) => count switch
    {
        0 => "",
        1 => OneFile,
        _ => string.Format(System.Globalization.CultureInfo.CurrentCulture, ManyFiles, count)
    };
}
