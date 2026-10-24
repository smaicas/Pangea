namespace CdCSharp.Pangea.Apps;

/// <summary>
/// An answer whose files were written for the caller, in a folder of the request's own.
/// </summary>
/// <remarks>
/// <para>
/// The convention for a request that produces files: the application answering writes them to a new
/// folder among the system's temporary files - not to the person's folders, so the request leaves
/// no trace there - and the folder and everything in it become the caller's. The caller moves or
/// copies what it wants and then deletes the folder, with <see cref="AppFilesResultExtensions.DeleteFolder"/>.
/// A folder a caller forgets is deleted by the application that wrote it, a while later.
/// </para>
/// <para>
/// Implement it on the result record of a protocol, beside the paths of the files themselves:
/// </para>
/// <code>
/// public sealed record ConvertResult(IReadOnlyList&lt;string&gt; Files) : IAppFilesResult
/// {
///     public string? Folder { get; init; }
/// }
/// </code>
/// </remarks>
public interface IAppFilesResult
{
    /// <summary>The folder holding the files, the caller's to delete; null when nothing was written.</summary>
    string? Folder { get; }
}

public static class AppFilesResultExtensions
{
    /// <summary>
    /// Deletes the result's folder and whatever is still in it, once the caller has taken what it
    /// wanted. Does nothing without a folder, and never throws: a folder that cannot be deleted now
    /// is deleted later by the application that wrote it.
    /// </summary>
    /// <returns>Whether the folder is gone.</returns>
    public static bool DeleteFolder(this IAppFilesResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (string.IsNullOrWhiteSpace(result.Folder)) return true;

        try
        {
            if (Directory.Exists(result.Folder)) Directory.Delete(result.Folder, recursive: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
