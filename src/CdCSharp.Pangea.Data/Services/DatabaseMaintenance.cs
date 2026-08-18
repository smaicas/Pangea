using CdCSharp.Pangea.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CdCSharp.Pangea.Data.Services;

/// <summary>
/// The implementation of <see cref="IDatabaseMaintenance{TContext}"/>. Everything specific to an
/// engine is asked of the provider; what is left - where backups go, what they are called, how many
/// are kept - is the same wherever the data ends up.
/// </summary>
internal sealed class DatabaseMaintenance<TContext> : IDatabaseMaintenance<TContext> where TContext : DbContext
{
    private const string BackupExtension = ".bak";

    private readonly IDbContextFactory<TContext> _factory;
    private readonly PangeaDbRuntime<TContext> _runtime;
    private readonly ILogger<DatabaseMaintenance<TContext>> _logger;

    public DatabaseMaintenance(
        IDbContextFactory<TContext> factory,
        PangeaDbRuntime<TContext> runtime,
        ILogger<DatabaseMaintenance<TContext>> logger)
    {
        _factory = factory;
        _runtime = runtime;
        _logger = logger;
    }

    public async Task<DatabaseInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        await using TContext context = await _factory.CreateDbContextAsync(cancellationToken);

        string? path = _runtime.DatabaseFilePath;
        long? size = path is not null && File.Exists(path) ? new FileInfo(path).Length : null;

        bool canConnect = await context.Database.CanConnectAsync(cancellationToken);

        return new DatabaseInfo(
            _runtime.Provider.Name,
            path,
            size,
            canConnect,
            canConnect ? await ReadMigrationsAsync(context.Database.GetAppliedMigrationsAsync, cancellationToken) : [],
            await ReadMigrationsAsync(context.Database.GetPendingMigrationsAsync, cancellationToken));
    }

    /// <summary>
    /// Migration lists are for showing the user, so failing to read them is not worth failing the
    /// call over: a database built with <c>EnsureCreated</c> has no history table at all, and
    /// asking a project with no migrations for its pending ones throws rather than answering none.
    /// </summary>
    private async Task<IReadOnlyList<string>> ReadMigrationsAsync(
        Func<CancellationToken, Task<IEnumerable<string>>> read, CancellationToken cancellationToken)
    {
        try
        {
            return (await read(cancellationToken)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "The migration history of {Context} could not be read", typeof(TContext).Name);
            return [];
        }
    }

    public async Task<string> BackupAsync(string? targetPath = null, CancellationToken cancellationToken = default)
    {
        bool automatic = targetPath is null;

        string target = targetPath ?? Path.Combine(
            _runtime.Locator.GetBackupDirectory(_runtime.Options),
            $"{BackupStem()}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}{BackupExtension}");

        await using (TContext context = await _factory.CreateDbContextAsync(cancellationToken))
        {
            await _runtime.Provider.BackupAsync(context, target, cancellationToken);
        }

        _logger.LogInformation("Backed up {Context} to {Path}", typeof(TContext).Name, target);

        // Only the ones this made: a path the application chose is the application's to look after.
        if (automatic) Prune(target);

        return target;
    }

    public async Task RestoreAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);

        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException($"There is no backup at '{backupPath}'.", backupPath);
        }

        await _runtime.Provider.RestoreAsync(_runtime.ConnectionString, backupPath, cancellationToken);

        _logger.LogWarning("Restored {Context} from {Path}", typeof(TContext).Name, backupPath);
    }

    public async Task CompactAsync(CancellationToken cancellationToken = default)
    {
        await using TContext context = await _factory.CreateDbContextAsync(cancellationToken);
        await _runtime.Provider.CompactAsync(context, cancellationToken);
    }

    public IReadOnlyList<string> GetBackups()
    {
        string directory = _runtime.Locator.GetBackupDirectory(_runtime.Options);

        if (!Directory.Exists(directory)) return [];

        return Directory
            .EnumerateFiles(directory, $"{BackupStem()}-*{BackupExtension}")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
    }

    /// <summary>
    /// What automatic backups are named after: the database file, or the context when the database
    /// is not a file. Also what tells one context's backups from another's in a shared folder.
    /// </summary>
    private string BackupStem() =>
        _runtime.DatabaseFilePath is { } path ? Path.GetFileNameWithoutExtension(path) : typeof(TContext).Name;

    /// <summary>
    /// Deletes the automatic backups beyond the configured number.
    /// </summary>
    /// <remarks>
    /// At least one is kept whatever the option says, and the copy just written is never one of the
    /// deleted. "Keep none" cannot be allowed to mean "delete the backup this call just made":
    /// <c>MigrationStrategy.MigrateWithBackup</c> takes one and then migrates, and a migration that
    /// fails after the pruning deleted its safety net turns a recoverable failure into a lost
    /// database reported as a missing file.
    /// </remarks>
    private void Prune(string justWritten)
    {
        int keep = Math.Max(1, _runtime.Options.BackupsToKeep);

        foreach (string old in GetBackups().Skip(keep))
        {
            // Belt and braces: the new backup is the newest, so it is inside the ones kept - unless
            // two backups share a write time to the tick and sort the other way round.
            if (string.Equals(old, justWritten, StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                File.Delete(old);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // An old backup that will not delete is worth a line in the log and nothing more:
                // the new one is already written, which is what the caller asked for.
                _logger.LogWarning(ex, "The old backup {Path} could not be deleted", old);
            }
        }
    }
}
