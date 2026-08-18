using CdCSharp.Pangea.Data.Abstractions;
using CdCSharp.Pangea.Data.Configuration;
using CdCSharp.Pangea.Data.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Data.Tests;

/// <summary>
/// What startup does to the database, which is the part of this feature that can destroy something:
/// the migration runs on the user's machine, unattended, with no way to answer a prompt.
/// </summary>
public class DatabaseLifecycleTests
{
    [Fact]
    public async Task Migrate_AppliesTheMigrationsAndRecordsThem()
    {
        await using ServiceProvider services = DataHost.Build<MigratedContext>(
            DataHost.NewDirectory(), db => db.Options.Migration = MigrationStrategy.Migrate);

        await DataHost.StartAsync(services, TestContext.Current.CancellationToken);

        DatabaseInfo info = await services.GetRequiredService<IDatabaseMaintenance<MigratedContext>>()
            .GetInfoAsync(TestContext.Current.CancellationToken);

        Assert.Equal("SQLite", info.ProviderName);
        Assert.Contains("0001_Initial", info.AppliedMigrations);
        Assert.Empty(info.PendingMigrations);
        Assert.True(info.CanConnect);
    }

    [Fact]
    public async Task None_LeavesTheDatabaseAlone()
    {
        await using ServiceProvider services = DataHost.Build<NotesContext>(
            DataHost.NewDirectory(), db => db.Options.Migration = MigrationStrategy.None);

        await DataHost.StartAsync(services, TestContext.Current.CancellationToken);

        IPangeaDbContext<NotesContext> data = services.GetRequiredService<IPangeaDbContext<NotesContext>>();

        // No schema was created, so the first query is the one that says so.
        await Assert.ThrowsAnyAsync<Exception>(() => data.ReadAsync(
            (context, token) => context.Notes.CountAsync(token), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The failure this whole strategy exists for: a migration that does not work, on a database
    /// with the user's data already in it.
    /// </summary>
    [Fact]
    public async Task MigrateWithBackup_PutsTheDataBackWhenTheMigrationFails()
    {
        string root = DataHost.NewDirectory();
        const string fileName = "shared.db";

        // The application as it shipped: a database with something in it worth keeping.
        await using (ServiceProvider before = DataHost.Build<NotesContext>(root, db =>
                     {
                         db.Options.DatabaseFileName = fileName;
                         db.Options.Migration = MigrationStrategy.EnsureCreated;
                     }))
        {
            await DataHost.StartAsync(before, TestContext.Current.CancellationToken);

            await before.GetRequiredService<IPangeaDbContext<NotesContext>>().WriteAsync((context, _) =>
            {
                context.Notes.Add(new Note { Title = "the user's work" });
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);
        }

        // The next version, whose migration is broken.
        await using (ServiceProvider upgrade = DataHost.Build<BrokenMigrationContext>(root, db =>
                     {
                         db.Options.DatabaseFileName = fileName;
                         db.Options.Migration = MigrationStrategy.MigrateWithBackup;
                     }))
        {
            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => DataHost.StartAsync(upgrade, TestContext.Current.CancellationToken));

            Assert.Contains("restored", failure.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(failure.InnerException);
        }

        // And the data is still there, which is the only assertion the user would care about.
        await using ServiceProvider after = DataHost.Build<NotesContext>(root, db =>
        {
            db.Options.DatabaseFileName = fileName;
            db.Options.Migration = MigrationStrategy.None;
        });

        List<Note> notes = await after.GetRequiredService<IPangeaDbContext<NotesContext>>().ReadAsync(
            (context, token) => context.Notes.ToListAsync(token), TestContext.Current.CancellationToken);

        Assert.Equal("the user's work", Assert.Single(notes).Title);
    }

    [Fact]
    public async Task SeedersRunAfterTheSchema_InOrder_AndTheirChangesAreSaved()
    {
        await using ServiceProvider services = DataHost.Build<NotesContext>(
            DataHost.NewDirectory(),
            db => db.Options.Migration = MigrationStrategy.EnsureCreated,
            registrations =>
            {
                registrations.AddSingleton<IDataSeeder<NotesContext>>(new TitleSeeder("second", 2));
                registrations.AddSingleton<IDataSeeder<NotesContext>>(new TitleSeeder("first", 1));
            });

        await DataHost.StartAsync(services, TestContext.Current.CancellationToken);

        List<string> titles = await services.GetRequiredService<IPangeaDbContext<NotesContext>>().ReadAsync(
            (context, token) => context.Notes.OrderBy(note => note.Id).Select(note => note.Title).ToListAsync(token),
            TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second"], titles);
    }

    private sealed class TitleSeeder : IDataSeeder<NotesContext>
    {
        private readonly string _title;

        public TitleSeeder(string title, int order)
        {
            _title = title;
            Order = order;
        }

        public int Order { get; }

        public Task SeedAsync(NotesContext context, CancellationToken cancellationToken)
        {
            // Nothing saved here: the initializer saves what a seeder leaves pending.
            context.Notes.Add(new Note { Title = _title });
            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// The operations an application has to be able to offer its user, because there is nobody else
/// standing behind a desktop database.
/// </summary>
public class DatabaseMaintenanceTests
{
    [Fact]
    public async Task InfoDescribesTheFileOnDisk()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        DatabaseInfo info = await database.Maintenance.GetInfoAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(info.FilePath);
        Assert.True(File.Exists(info.FilePath));
        Assert.True(info.SizeBytes > 0);
        Assert.True(info.CanConnect);
    }

    [Fact]
    public async Task ABackupIsAWorkingDatabase_AndRestoringItUndoesWhatCameAfter()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        await database.Db.WriteAsync((context, _) =>
        {
            context.Notes.Add(new Note { Title = "kept" });
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        string backup = await database.Maintenance.BackupAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(backup));

        await database.Db.WriteAsync((context, _) =>
        {
            context.Notes.Add(new Note { Title = "lost" });
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        await database.Maintenance.RestoreAsync(backup, TestContext.Current.CancellationToken);

        List<string> titles = await database.Db.ReadAsync(
            (context, token) => context.Notes.Select(note => note.Title).ToListAsync(token),
            TestContext.Current.CancellationToken);

        Assert.Equal(["kept"], titles);
    }

    [Fact]
    public async Task AutomaticBackupsAreKeptToTheConfiguredNumber()
    {
        await using PangeaTestDatabase<NotesContext> database =
            await PangeaTestDatabase<NotesContext>.CreateAsync(db => db.Options.BackupsToKeep = 2, cancellationToken: TestContext.Current.CancellationToken);

        for (int index = 0; index < 4; index++)
        {
            await database.Maintenance.BackupAsync(cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, database.Maintenance.GetBackups().Count);
    }

    [Fact]
    public async Task RestoringSomethingThatIsNotThere_SaysSo()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<FileNotFoundException>(() => database.Maintenance.RestoreAsync(
            Path.Combine(database.DirectoryPath, "no-such-backup.bak"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompactingLeavesTheDataReadable()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        await database.Db.WriteAsync((context, _) =>
        {
            context.Notes.AddRange(Enumerable.Range(0, 200).Select(index => new Note { Title = $"note {index}" }));
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        await database.Db.WriteAsync(
            (context, token) => context.Notes.Where(note => note.Id > 20).ExecuteDeleteAsync(token),
            TestContext.Current.CancellationToken);

        await database.Maintenance.CompactAsync(TestContext.Current.CancellationToken);

        Assert.Equal(20, await database.Db.ReadAsync(
            (context, token) => context.Notes.CountAsync(token), TestContext.Current.CancellationToken));
    }
}
