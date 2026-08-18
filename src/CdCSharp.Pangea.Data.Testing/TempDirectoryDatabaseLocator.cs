using CdCSharp.Pangea.Data.Abstractions;
using CdCSharp.Pangea.Data.Configuration;

namespace CdCSharp.Pangea.Data.Testing;

/// <summary>
/// Puts the database in a directory of the test's own instead of in the user's data folder.
/// </summary>
/// <remarks>
/// Standing in for the storage-backed locator, which resolves to the real per-platform data
/// directory: a test suite that wrote there would collide with the developer's own copy of the
/// application and leave its databases behind.
/// </remarks>
public sealed class TempDirectoryDatabaseLocator : IDatabaseLocator
{
    private readonly string _root;

    public TempDirectoryDatabaseLocator(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    public string GetDatabaseFilePath(PangeaDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string path = string.IsNullOrWhiteSpace(options.DatabasePath)
            ? Path.Combine(_root, options.DatabaseFileName)
            : Path.GetFullPath(options.DatabasePath);

        // The same promise the storage-backed locator makes, because a database name may name a
        // folder too. Without it the harness fails on a configuration an application handles.
        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        return path;
    }

    public string GetBackupDirectory(PangeaDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string directory = string.IsNullOrWhiteSpace(options.BackupDirectory)
            ? Path.Combine(_root, "backups")
            : Path.GetFullPath(options.BackupDirectory);

        Directory.CreateDirectory(directory);
        return directory;
    }
}
