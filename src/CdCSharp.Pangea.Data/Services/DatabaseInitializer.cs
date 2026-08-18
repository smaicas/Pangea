using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Data.Abstractions;
using CdCSharp.Pangea.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CdCSharp.Pangea.Data.Services;

/// <summary>
/// Brings the database up to date before the application shows anything.
/// </summary>
/// <remarks>
/// <para>
/// A desktop application cannot be told to run <c>dotnet ef database update</c>: the machine the
/// schema is out of date on belongs to the user, and the only thing that runs there is the
/// application itself. So the migration happens on startup, which makes it the one piece of startup
/// that can destroy something - hence
/// <see cref="MigrationStrategy.MigrateWithBackup"/> as the default.
/// </para>
/// <para>
/// Registered as an <see cref="IPangeaAsyncInitializer"/>, so it is awaited behind the splash
/// window rather than blocking the UI thread or racing the first screen.
/// </para>
/// </remarks>
internal sealed class DatabaseInitializer<TContext> : IPangeaAsyncInitializer where TContext : DbContext
{
    private readonly IDbContextFactory<TContext> _factory;
    private readonly PangeaDbRuntime<TContext> _runtime;
    private readonly IDatabaseMaintenance<TContext> _maintenance;
    private readonly IEnumerable<IDataSeeder<TContext>> _seeders;
    private readonly ILogger<DatabaseInitializer<TContext>> _logger;

    public DatabaseInitializer(
        IDbContextFactory<TContext> factory,
        PangeaDbRuntime<TContext> runtime,
        IDatabaseMaintenance<TContext> maintenance,
        IEnumerable<IDataSeeder<TContext>> seeders,
        ILogger<DatabaseInitializer<TContext>> logger)
    {
        _factory = factory;
        _runtime = runtime;
        _maintenance = maintenance;
        _seeders = seeders;
        _logger = logger;
    }

    public string Name => $"Preparing {typeof(TContext).Name}";

    public int Order => _runtime.Options.StartupOrder;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await ApplySchemaAsync(cancellationToken);

        await using (TContext context = await _factory.CreateDbContextAsync(cancellationToken))
        {
            await _runtime.Provider.PrepareAsync(context, cancellationToken);
        }

        await SeedAsync(cancellationToken);
    }

    private async Task ApplySchemaAsync(CancellationToken cancellationToken)
    {
        switch (_runtime.Options.Migration)
        {
            case MigrationStrategy.None:
                return;

            case MigrationStrategy.EnsureCreated:
                await using (TContext context = await _factory.CreateDbContextAsync(cancellationToken))
                {
                    await context.Database.EnsureCreatedAsync(cancellationToken);
                }

                return;

            case MigrationStrategy.Migrate:
                await MigrateAsync(cancellationToken);
                return;

            case MigrationStrategy.MigrateWithBackup:
                await MigrateWithBackupAsync(cancellationToken);
                return;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(_runtime.Options.Migration), _runtime.Options.Migration, "Unknown migration strategy.");
        }
    }

    private async Task MigrateWithBackupAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> pending = await GetPendingMigrationsAsync(cancellationToken);

        if (pending.Count == 0) return;

        string? databasePath = _runtime.DatabaseFilePath;

        // Nothing to lose on a database that does not exist yet, and nothing to copy either.
        bool worthBackingUp = databasePath is not null && File.Exists(databasePath);

        string? backup = worthBackingUp ? await _maintenance.BackupAsync(null, cancellationToken) : null;

        _logger.LogInformation(
            "Applying {Count} migration(s) to {Context}: {Migrations}",
            pending.Count, typeof(TContext).Name, string.Join(", ", pending));

        try
        {
            await MigrateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (backup is null) throw;

            _logger.LogError(ex, "Migrating {Context} failed; restoring the backup", typeof(TContext).Name);

            try
            {
                await _maintenance.RestoreAsync(backup, cancellationToken);
            }
            catch (Exception restoreFailure)
            {
                // Both failures matter and neither explains the other: the migration failed, and
                // the database is now whatever the failed migration left behind.
                throw new AggregateException(
                    $"Migrating '{typeof(TContext).Name}' failed and the backup at '{backup}' could not be " +
                    "restored. The database is in the state the failed migration left it in.",
                    ex, restoreFailure);
            }

            throw new InvalidOperationException(
                $"Migrating '{typeof(TContext).Name}' failed. The database was restored from '{backup}'.", ex);
        }
    }

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using TContext context = await _factory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken)
    {
        await using TContext context = await _factory.CreateDbContextAsync(cancellationToken);
        return (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (IDataSeeder<TContext> seeder in _seeders.OrderBy(seeder => seeder.Order))
        {
            await using TContext context = await _factory.CreateDbContextAsync(cancellationToken);

            await seeder.SeedAsync(context, cancellationToken);

            // Saved here, so a seeder is the query and the insert rather than the ceremony round
            // them. One that saves for itself leaves nothing to do and this is a no-op.
            if (context.ChangeTracker.HasChanges()) await context.SaveChangesAsync(cancellationToken);
        }
    }
}
