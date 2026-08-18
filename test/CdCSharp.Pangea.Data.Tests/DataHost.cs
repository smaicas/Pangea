using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Data.Abstractions;
using CdCSharp.Pangea.Data.Configuration;
using CdCSharp.Pangea.Data.Sqlite;
using CdCSharp.Pangea.Data.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Data.Tests;

/// <summary>
/// A container holding one registered database, for the tests that need two of them over the same
/// file - which is what a migration failure looks like from the outside: the application that
/// wrote the data and the version of it that tried to change the schema.
/// </summary>
internal static class DataHost
{
    public static string NewDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "pangea-data-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static ServiceProvider Build<TContext>(
        string root,
        Action<PangeaDbBuilder>? configure = null,
        Action<IServiceCollection>? configureServices = null) where TContext : DbContext
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IDatabaseLocator>(new TempDirectoryDatabaseLocator(root));

        services.AddPangeaDbContext<TContext>(db =>
        {
            db.UseSqlite();
            configure?.Invoke(db);
        });

        configureServices?.Invoke(services);

        return services.BuildServiceProvider();
    }

    /// <summary>Runs startup the way <c>StartupSequence</c> does, in order.</summary>
    public static async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        foreach (IPangeaAsyncInitializer initializer in services
                     .GetServices<IPangeaAsyncInitializer>()
                     .OrderBy(initializer => initializer.Order))
        {
            await initializer.InitializeAsync(cancellationToken);
        }
    }
}
