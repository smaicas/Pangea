using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Data.Abstractions;
using CdCSharp.Pangea.Data.Configuration;
using CdCSharp.Pangea.Data.Services;
using CdCSharp.Pangea.Data.Sqlite;
using CdCSharp.Pangea.Data.Testing;
using CdCSharp.Pangea.Storage;
using CdCSharp.Pangea.Storage.Providers;
using CdCSharp.Pangea.Storage.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Data.Tests;

/// <summary>
/// What registering a database does, and what it refuses to do. The refusals matter as much: an
/// application that forgets the provider should be told which package it is missing, not handed a
/// container that fails at the first query.
/// </summary>
public class RegistrationTests
{
    [Fact]
    public void WithoutAProvider_TheErrorNamesThePackageAndTheCall()
    {
        ServiceCollection services = new();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => services.AddPangeaDbContext<NotesContext>(_ => { }));

        Assert.Contains("CdCSharp.Pangea.Data.Sqlite", error.Message, StringComparison.Ordinal);
        Assert.Contains("UseSqlite", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ChoosingTwoProvidersForOneContext_IsRejected()
    {
        ServiceCollection services = new();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            services.AddPangeaDbContext<NotesContext>(db =>
            {
                db.UseSqlite();
                db.UseSqlite();
            }));

        Assert.Contains("SQLite", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheContextIsRegisteredThroughAFactory_NotAsAContext()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IDatabaseLocator>(new TempDirectoryDatabaseLocator(NewTempDirectory()));
        services.AddPangeaDbContext<NotesContext>(db => db.UseSqlite());

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IDbContextFactory<NotesContext>>());
        Assert.NotNull(provider.GetService<IPangeaDbContext<NotesContext>>());
        Assert.NotNull(provider.GetService<IDatabaseMaintenance<NotesContext>>());

        // A context resolved from the container would be one whose lifetime nobody owns, which is
        // the mistake this feature exists to make unavailable.
        Assert.Null(provider.GetService<NotesContext>());
    }

    [Fact]
    public void PreparingTheDatabaseIsRegisteredAsAStartupInitializer_AndRunsBeforeTheApplicationsOwn()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IDatabaseLocator>(new TempDirectoryDatabaseLocator(NewTempDirectory()));
        services.AddPangeaDbContext<NotesContext>(db => db.UseSqlite());

        using ServiceProvider provider = services.BuildServiceProvider();

        IPangeaAsyncInitializer initializer = Assert.Single(provider.GetServices<IPangeaAsyncInitializer>());

        Assert.Contains("NotesContext", initializer.Name, StringComparison.Ordinal);
        Assert.True(initializer.Order < 0, "The database should be ready before anything the application registers.");
    }

    [Fact]
    public void TheLocatorPutsTheDatabaseInThePerPlatformDataFolder_AndCreatesIt()
    {
        StorageOptions storage = new()
        {
            UsePortableMode = true,
            CustomDataPath = NewTempDirectory(),
            ApplicationName = "LocatorProbe"
        };

        StorageDatabaseLocator locator = new(
            new StorageService(new PortablePlatformPathProvider(Options.Create(storage))));

        string path = locator.GetDatabaseFilePath(new PangeaDbOptions { DatabaseFileName = "probe.db" });

        Assert.EndsWith("probe.db", path, StringComparison.Ordinal);
        Assert.True(Directory.Exists(Path.GetDirectoryName(path)),
            "SQLite will not create the folder its file goes in, so the locator has to.");
    }

    [Fact]
    public void AnAbsoluteDatabasePathOverridesTheDataFolder()
    {
        string chosen = Path.Combine(NewTempDirectory(), "elsewhere", "chosen.db");

        StorageOptions storage = new() { UsePortableMode = true, CustomDataPath = NewTempDirectory() };

        StorageDatabaseLocator locator = new(
            new StorageService(new PortablePlatformPathProvider(Options.Create(storage))));

        Assert.Equal(
            Path.GetFullPath(chosen),
            locator.GetDatabaseFilePath(new PangeaDbOptions { DatabasePath = chosen }));
    }

    [Fact]
    public void BackupsGoBesideTheDatabase()
    {
        string root = NewTempDirectory();
        TempDirectoryDatabaseLocator locator = new(root);

        string backups = locator.GetBackupDirectory(new PangeaDbOptions());

        Assert.Equal(Path.Combine(root, "backups"), backups);
        Assert.True(Directory.Exists(backups));
    }

    private static string NewTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "pangea-data-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
