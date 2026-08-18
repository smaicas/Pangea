using CdCSharp.Pangea.Data.Abstractions;
using CdCSharp.Pangea.Data.Configuration;
using CdCSharp.Pangea.Storage.Abstractions;

namespace CdCSharp.Pangea.Data.Services;

/// <summary>
/// Puts the database in the folder the storage feature keeps this application's data in.
/// </summary>
/// <remarks>
/// Both methods create the directory they name. SQLite does not: pointed at a file in a folder that
/// is not there, it fails to open with an error naming the file rather than the folder, on the
/// first run of a freshly installed application and never again on the developer's machine.
/// </remarks>
internal sealed class StorageDatabaseLocator : IDatabaseLocator
{
    private readonly IStorageService _storage;

    public StorageDatabaseLocator(IStorageService storage) => _storage = storage;

    public string GetDatabaseFilePath(PangeaDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string path = string.IsNullOrWhiteSpace(options.DatabasePath)
            ? _storage.GetDataFilePath(options.DatabaseFileName)
            : Path.GetFullPath(options.DatabasePath);

        EnsureDirectoryOf(path);
        return path;
    }

    public string GetBackupDirectory(PangeaDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string directory = string.IsNullOrWhiteSpace(options.BackupDirectory)
            ? Path.Combine(Path.GetDirectoryName(GetDatabaseFilePath(options))!, "backups")
            : Path.GetFullPath(options.BackupDirectory);

        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void EnsureDirectoryOf(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    }
}
