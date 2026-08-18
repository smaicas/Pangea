using CdCSharp.Pangea.Data.Configuration;

namespace CdCSharp.Pangea.Data.Abstractions;

/// <summary>
/// Where a file-backed database and its backups live on this machine.
/// </summary>
/// <remarks>
/// The reason this feature exists rather than a bare <c>UseSqlite</c> call: a desktop application's
/// database belongs in the per-platform data directory, which is <c>%APPDATA%</c> on Windows,
/// <c>~/.config</c> on Linux and <c>~/Library/Application Support</c> on macOS - or beside the
/// executable when the application runs portable. The storage feature already knows which.
/// </remarks>
public interface IDatabaseLocator
{
    /// <summary>The absolute path of the database file.</summary>
    string GetDatabaseFilePath(PangeaDbOptions options);

    /// <summary>The directory backups are written to. Created if it is not there yet.</summary>
    string GetBackupDirectory(PangeaDbOptions options);
}
