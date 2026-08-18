using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Data.Abstractions;
using CdCSharp.Pangea.Data.Configuration;
using CdCSharp.Pangea.Data.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Data.Testing;

/// <summary>
/// A real SQLite database in a directory of its own, wired the way the application wires one, and
/// deleted when the test is done with it.
/// </summary>
/// <remarks>
/// <para>
/// The registration path is the application's: <c>AddPangeaDbContext</c>, the same provider, the
/// same initializer. What changes is where the file goes. So a test exercises the feature rather
/// than a rehearsal of it, and a failure here is a failure the application would have had.
/// </para>
/// <para>
/// Contexts registered this way default to <see cref="MigrationStrategy.EnsureCreated"/>, because a
/// context written for a test usually has no migrations at all. Pass
/// <c>db.Options.Migration = MigrationStrategy.Migrate</c> to exercise the real ones.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await using PangeaTestDatabase&lt;AppDbContext&gt; database = await PangeaTestDatabase&lt;AppDbContext&gt;.CreateAsync();
///
/// await database.Db.WriteAsync((context, token) =>
/// {
///     context.Add(new Note { Title = "first" });
///     return Task.CompletedTask;
/// });
///
/// Assert.Equal(1, await database.Db.ReadAsync((context, token) => context.Set&lt;Note&gt;().CountAsync(token)));
/// </code>
/// </example>
public sealed class PangeaTestDatabase<TContext> : IAsyncDisposable where TContext : DbContext
{
    private readonly ServiceProvider _services;

    private PangeaTestDatabase(ServiceProvider services, string directoryPath)
    {
        _services = services;
        DirectoryPath = directoryPath;
    }

    /// <summary>The container the database was built from, for resolving seeders or maintenance.</summary>
    public IServiceProvider Services => _services;

    /// <summary>The directory holding the database and its backups. Deleted on disposal.</summary>
    public string DirectoryPath { get; }

    /// <summary>How a view model would reach this database.</summary>
    public IPangeaDbContext<TContext> Db => _services.GetRequiredService<IPangeaDbContext<TContext>>();

    public IDatabaseMaintenance<TContext> Maintenance => _services.GetRequiredService<IDatabaseMaintenance<TContext>>();

    /// <summary>
    /// Builds the database and runs every startup initializer against it, so it comes back with its
    /// schema in place and its seeders already run.
    /// </summary>
    /// <remarks>
    /// <paramref name="configure"/> is applied after the SQLite provider is chosen, so it can
    /// change any option; <paramref name="configureServices"/> runs afterwards, for the seeders and
    /// the services under test.
    /// </remarks>
    public static async Task<PangeaTestDatabase<TContext>> CreateAsync(
        Action<PangeaDbBuilder>? configure = null,
        Action<IServiceCollection>? configureServices = null,
        CancellationToken cancellationToken = default)
    {
        string root = Path.Combine(
            Path.GetTempPath(), "pangea-data-tests", Guid.NewGuid().ToString("N"));

        ServiceCollection services = new();
        services.AddLogging();

        // Before AddPangeaDbContext, which only fills in the storage-backed locator if nothing
        // else claimed the registration.
        services.AddSingleton<IDatabaseLocator>(new TempDirectoryDatabaseLocator(root));

        services.AddPangeaDbContext<TContext>(db =>
        {
            db.UseSqlite();
            db.Options.Migration = MigrationStrategy.EnsureCreated;
            configure?.Invoke(db);
        });

        configureServices?.Invoke(services);

        ServiceProvider provider = services.BuildServiceProvider();
        PangeaTestDatabase<TContext> database = new(provider, root);

        try
        {
            foreach (IPangeaAsyncInitializer initializer in provider
                         .GetServices<IPangeaAsyncInitializer>()
                         .OrderBy(initializer => initializer.Order))
            {
                await initializer.InitializeAsync(cancellationToken);
            }
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }

        return database;
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();

        // The pool keeps connections - and therefore file handles - open after the container has
        // let go of them, and on Windows an open handle is a directory that will not delete.
        SqliteConnection.ClearAllPools();

        try
        {
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A temporary directory that outlives the test is litter, not a failure, and failing
            // the test over it would report the cleanup instead of the thing under test.
        }
    }
}
