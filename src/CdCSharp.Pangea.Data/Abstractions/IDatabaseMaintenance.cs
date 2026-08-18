using Microsoft.EntityFrameworkCore;

namespace CdCSharp.Pangea.Data.Abstractions;

/// <summary>What the application can say about its database, and do to it.</summary>
/// <param name="ProviderName">The engine, as named by its provider.</param>
/// <param name="FilePath">The file it lives in, or <see langword="null"/> for a server.</param>
/// <param name="SizeBytes">The size of that file, or <see langword="null"/> when there is none.</param>
/// <param name="CanConnect">Whether the database answered.</param>
/// <param name="AppliedMigrations">Migrations already in the database, oldest first.</param>
/// <param name="PendingMigrations">Migrations the code has and the database does not.</param>
public sealed record DatabaseInfo(
    string ProviderName,
    string? FilePath,
    long? SizeBytes,
    bool CanConnect,
    IReadOnlyList<string> AppliedMigrations,
    IReadOnlyList<string> PendingMigrations);

/// <summary>
/// The operations a desktop application has to be able to offer its user itself.
/// </summary>
/// <remarks>
/// There is no database administrator behind a desktop application. Whatever the user is going to
/// be told to do when something goes wrong has to be a button in the application, which means the
/// application needs to be able to say how big the database is, take a copy of it, put a copy back
/// and reclaim space - without anyone opening a shell.
/// </remarks>
public interface IDatabaseMaintenance<TContext> where TContext : DbContext
{
    Task<DatabaseInfo> GetInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies the database. Returns the path written to.
    /// </summary>
    /// <remarks>
    /// With no target path, it writes a timestamped file to the backup directory and deletes the
    /// oldest automatic backups beyond <c>PangeaDbOptions.BackupsToKeep</c>. A path given here is
    /// left alone: a copy the application asked for by name is the application's to look after.
    /// </remarks>
    Task<string> BackupAsync(string? targetPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the database with a backup.
    /// </summary>
    /// <remarks>
    /// Nothing may be reading it: contexts already handed out keep their connections, and this
    /// replaces the file underneath them. Restart, or restore before the first query.
    /// </remarks>
    Task RestoreAsync(string backupPath, CancellationToken cancellationToken = default);

    /// <summary>Reclaims the space deleted rows left behind.</summary>
    Task CompactAsync(CancellationToken cancellationToken = default);

    /// <summary>The backups in the backup directory, newest first.</summary>
    IReadOnlyList<string> GetBackups();
}
