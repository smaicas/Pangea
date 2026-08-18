using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Data.Abstractions;
using CdCSharp.Pangea.Data.Configuration;
using CdCSharp.Pangea.Data.Sqlite;
using CdCSharp.Pangea.Data.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Data.Tests;

/// <summary>
/// The edges the first round of tests walked past. Each of these was a defect found by reading the
/// code rather than by using it, which is the only time some of them would ever have been found:
/// they are all on paths an application takes once, on a machine nobody is watching.
/// </summary>
public class RegressionTests
{
    /// <summary>
    /// Keeping "zero" backups used to delete the backup that had just been written - including the
    /// one <see cref="MigrationStrategy.MigrateWithBackup"/> takes moments before it migrates, which
    /// turned a failed migration from recoverable into data loss reported as a missing file.
    /// </summary>
    [Fact]
    public async Task AnAutomaticBackupSurvivesItsOwnPruning_WhateverTheLimitSaysToKeep()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(
            db => db.Options.BackupsToKeep = 0, cancellationToken: TestContext.Current.CancellationToken);

        string backup = await database.Maintenance.BackupAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(backup), "The backup just written was deleted by the pruning that followed it.");
        Assert.Contains(backup, database.Maintenance.GetBackups());
    }

    /// <summary>
    /// The same failure, from the direction that made it matter: a migration that fails on a
    /// database whose owner asked to keep no backups.
    /// </summary>
    [Fact]
    public async Task AFailedMigration_IsStillRecoverable_WhenNoBackupsAreKept()
    {
        string root = DataHost.NewDirectory();
        const string fileName = "kept-none.db";

        await using (ServiceProvider before = DataHost.Build<NotesContext>(root, db =>
                     {
                         db.Options.DatabaseFileName = fileName;
                         db.Options.Migration = MigrationStrategy.EnsureCreated;
                         db.Options.BackupsToKeep = 0;
                     }))
        {
            await DataHost.StartAsync(before, TestContext.Current.CancellationToken);

            await before.GetRequiredService<IPangeaDbContext<NotesContext>>().WriteAsync((context, _) =>
            {
                context.Notes.Add(new Note { Title = "survives" });
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);
        }

        await using (ServiceProvider upgrade = DataHost.Build<BrokenMigrationContext>(root, db =>
                     {
                         db.Options.DatabaseFileName = fileName;
                         db.Options.Migration = MigrationStrategy.MigrateWithBackup;
                         db.Options.BackupsToKeep = 0;
                     }))
        {
            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => DataHost.StartAsync(upgrade, TestContext.Current.CancellationToken));

            Assert.Contains("restored", failure.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using ServiceProvider after = DataHost.Build<NotesContext>(root, db =>
        {
            db.Options.DatabaseFileName = fileName;
            db.Options.Migration = MigrationStrategy.None;
        });

        List<Note> notes = await after.GetRequiredService<IPangeaDbContext<NotesContext>>().ReadAsync(
            (context, token) => context.Notes.ToListAsync(token), TestContext.Current.CancellationToken);

        Assert.Equal("survives", Assert.Single(notes).Title);
    }

    /// <summary>
    /// A backup the application asked for by name is its own to look after, so it is never pruned.
    /// </summary>
    [Fact]
    public async Task ABackupWrittenToAChosenPath_IsLeftAlone()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(
            db => db.Options.BackupsToKeep = 1, cancellationToken: TestContext.Current.CancellationToken);

        string chosen = Path.Combine(database.DirectoryPath, "exports", "chosen.bak");

        await database.Maintenance.BackupAsync(chosen, TestContext.Current.CancellationToken);
        await database.Maintenance.BackupAsync(cancellationToken: TestContext.Current.CancellationToken);
        await database.Maintenance.BackupAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(chosen));
        Assert.Single(database.Maintenance.GetBackups());
    }

    /// <summary>
    /// A blank connection string names no file, which is the answer the interface documents. It
    /// used to throw, from inside the startup path that asks whether there is a file to prepare.
    /// </summary>
    [Fact]
    public void AConnectionStringThatNamesNothing_HasNoFile()
    {
        SqliteDbProvider provider = new();

        Assert.Null(provider.GetDatabaseFilePath(string.Empty));
        Assert.Null(provider.GetDatabaseFilePath("   "));
    }

    /// <summary>
    /// Registering the same context twice is a mistake with no symptom: the second registration is
    /// mostly swallowed, and what survives is a second startup initializer migrating the same
    /// database again.
    /// </summary>
    [Fact]
    public void RegisteringOneContextTwice_IsRejected()
    {
        ServiceCollection services = new();
        services.AddPangeaDbContext<NotesContext>(db => db.UseSqlite());

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => services.AddPangeaDbContext<NotesContext>(db => db.UseSqlite()));

        Assert.Contains(nameof(NotesContext), error.Message, StringComparison.Ordinal);
    }

    /// <summary>Two databases in one application, which is the reason any of this is generic.</summary>
    [Fact]
    public async Task TwoContextsSideBySide_KeepTheirOwnFilesAndTheirOwnInitializers()
    {
        string root = DataHost.NewDirectory();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IDatabaseLocator>(new TempDirectoryDatabaseLocator(root));

        services.AddPangeaDbContext<NotesContext>(db =>
        {
            db.UseSqlite("notes.db");
            db.Options.Migration = MigrationStrategy.EnsureCreated;
        });

        services.AddPangeaDbContext<MigratedContext>(db =>
        {
            db.UseSqlite("archive.db");
            db.Options.Migration = MigrationStrategy.Migrate;
        });

        await using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Equal(2, provider.GetServices<IPangeaAsyncInitializer>().Count());

        await DataHost.StartAsync(provider, TestContext.Current.CancellationToken);

        await provider.GetRequiredService<IPangeaDbContext<NotesContext>>().WriteAsync((context, _) =>
        {
            context.Notes.Add(new Note { Title = "in notes.db" });
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        // Separate files, so what one holds says nothing about the other.
        Assert.Equal(0, await provider.GetRequiredService<IPangeaDbContext<MigratedContext>>().ReadAsync(
            (context, token) => context.Notes.CountAsync(token), TestContext.Current.CancellationToken));

        Assert.True(File.Exists(Path.Combine(root, "notes.db")));
        Assert.True(File.Exists(Path.Combine(root, "archive.db")));
    }

    /// <summary>
    /// A database name with a folder in it. The storage-backed locator creates the folder because
    /// SQLite will not; the one the tests run on has to agree, or the harness fails where an
    /// application would work.
    /// </summary>
    [Fact]
    public async Task ADatabaseNameWithAFolderInIt_Works()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(
            db => db.Options.DatabaseFileName = Path.Combine("data", "notes.db"),
            cancellationToken: TestContext.Current.CancellationToken);

        await database.Db.WriteAsync((context, _) =>
        {
            context.Notes.Add(new Note { Title = "nested" });
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(database.DirectoryPath, "data", "notes.db")));
    }

    /// <summary>
    /// The write lock is taken by every write and has to be given back by every write, including
    /// the ones that throw. Without that, one failed save deadlocks every save after it - and the
    /// application looks frozen rather than broken.
    /// </summary>
    [Fact]
    public async Task AFailedWrite_DoesNotHoldTheWriteLock()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => database.Db.WriteAsync(
            (context, token) => throw new InvalidOperationException("the write failed"),
            TestContext.Current.CancellationToken));

        Task<int> afterwards = database.Db.WriteAsync((context, _) =>
        {
            context.Notes.Add(new Note { Title = "after the failure" });
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(1, await afterwards.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A database created from the model has no migration history, and asking about one has to
    /// answer rather than throw: the settings screen shows this.
    /// </summary>
    [Fact]
    public async Task AnEnsureCreatedDatabase_ReportsNoMigrationsRatherThanFailing()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        DatabaseInfo info = await database.Maintenance.GetInfoAsync(TestContext.Current.CancellationToken);

        Assert.True(info.CanConnect);
        Assert.Empty(info.AppliedMigrations);
        Assert.Empty(info.PendingMigrations);
    }
}
