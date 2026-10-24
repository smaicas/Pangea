using Microsoft.Extensions.Logging;

namespace CdCSharp.Pangea.Apps;

/// <summary>
/// Where an application writes the files other applications ask it for: a folder of its own among
/// the system's temporary files, and one folder inside it per request.
/// </summary>
/// <remarks>
/// <para>
/// The answering side of <see cref="IAppFilesResult"/>. A request leaves no trace in the
/// application or in the person's folders: what is written here is the caller's, who moves it and
/// deletes the folder. The folders a caller forgot are deleted once they are older than
/// <see cref="Kept"/>, each time a new one is made.
/// </para>
/// <para>
/// Registered by <see cref="AppsFeature"/> under <c>&lt;temp&gt;/&lt;app id&gt;/requests</c>; a
/// handler asks for it in its constructor.
/// </para>
/// </remarks>
public sealed class AppRequestFolders
{
    /// <summary>How long a request's folder is left for its caller by default: a day.</summary>
    public static readonly TimeSpan DefaultKept = TimeSpan.FromDays(1);

    private readonly ILogger<AppRequestFolders> _logger;

    /// <param name="root">The folder that holds one folder per request. Created when the first is.</param>
    /// <param name="logger">Where a folder that could not be deleted is mentioned.</param>
    /// <param name="kept">How long a folder is left for its caller; <see cref="DefaultKept"/> when null.</param>
    public AppRequestFolders(string root, ILogger<AppRequestFolders> logger, TimeSpan? kept = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(logger);

        Root = Path.GetFullPath(root);
        Kept = kept ?? DefaultKept;
        _logger = logger;
    }

    /// <summary>The root an application uses unless told otherwise: <c>&lt;temp&gt;/&lt;app id&gt;/requests</c>.</summary>
    public static string DefaultRootFor(AppIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return Path.Combine(Path.GetTempPath(), identity.Id, "requests");
    }

    public string Root { get; }

    /// <summary>How long a request's folder is left for its caller before it is deleted.</summary>
    public TimeSpan Kept { get; }

    /// <summary>A new, empty folder for one request, after deleting the ones callers forgot.</summary>
    public string NewFolder()
    {
        DeleteForgotten();

        string folder = Path.Combine(Root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>
    /// Deletes a request's folder with whatever is in it: when the request failed or was cancelled,
    /// and nothing in it is anyone's. A folder outside <see cref="Root"/> is left alone.
    /// </summary>
    public void Delete(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !IsInside(folder)) return;

        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogInformation(ex, "Could not delete the request folder {Folder}", folder);
        }
    }

    /// <summary>Deletes the folders older than <see cref="Kept"/>: what their callers did not take.</summary>
    public void DeleteForgotten()
    {
        if (!Directory.Exists(Root)) return;

        DateTime before = DateTime.UtcNow - Kept;

        try
        {
            foreach (DirectoryInfo folder in new DirectoryInfo(Root).EnumerateDirectories().Where(folder => folder.CreationTimeUtc < before))
            {
                Delete(folder.FullName);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogInformation(ex, "Could not look through the request folders in {Root}", Root);
        }
    }

    private bool IsInside(string folder)
    {
        string full = Path.GetFullPath(folder);
        string root = Path.EndsInDirectorySeparator(Root) ? Root : Root + Path.DirectorySeparatorChar;

        return full.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
