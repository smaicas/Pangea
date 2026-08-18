using CdCSharp.Pangea.Data.Abstractions;
using CdCSharp.Pangea.Data.Configuration;
using CdCSharp.Pangea.Data.Sqlite;
using CdCSharp.Pangea.Data.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Data.Tests;

/// <summary>
/// The options and the provider contract, exercised down the branches an application takes when it
/// asks for something other than the defaults - which is where untested code hides.
/// </summary>
public class ProviderAndOptionsTests
{
    /// <summary>
    /// A provider that handles concurrent writers itself, which is what a server-backed one would
    /// say. It is also the only way to exercise the branch that does not queue writes, and the
    /// first real check that <see cref="IPangeaDbProvider"/> can be implemented outside the
    /// packages that ship one.
    /// </summary>
    private sealed class UnqueuedSqliteProvider : IPangeaDbProvider
    {
        private readonly SqliteDbProvider _inner = new();

        public string Name => "SQLite (unqueued)";

        public bool SerializesWrites => false;

        public string ResolveConnectionString(PangeaDbOptions options, IDatabaseLocator locator) =>
            _inner.ResolveConnectionString(options, locator);

        public void Configure(DbContextOptionsBuilder builder, string connectionString, PangeaDbOptions options) =>
            _inner.Configure(builder, connectionString, options);

        public string? GetDatabaseFilePath(string connectionString) => _inner.GetDatabaseFilePath(connectionString);

        public Task PrepareAsync(DbContext context, CancellationToken cancellationToken) =>
            _inner.PrepareAsync(context, cancellationToken);

        public Task BackupAsync(DbContext context, string targetPath, CancellationToken cancellationToken) =>
            _inner.BackupAsync(context, targetPath, cancellationToken);

        public Task RestoreAsync(string connectionString, string backupPath, CancellationToken cancellationToken) =>
            _inner.RestoreAsync(connectionString, backupPath, cancellationToken);

        public Task CompactAsync(DbContext context, CancellationToken cancellationToken) =>
            _inner.CompactAsync(context, cancellationToken);
    }

    private sealed class CountingSeeder : IDataSeeder<NotesContext>
    {
        public Task SeedAsync(NotesContext context, CancellationToken cancellationToken)
        {
            context.Notes.Add(new Note { Title = "seeded by the registration helper" });
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// The branch that does not take the write lock, because the provider says it does not need
    /// one. Writes still have to arrive.
    /// </summary>
    [Fact]
    public async Task AProviderThatHandlesItsOwnWriters_IsNotQueued()
    {
        ServiceCollection collection = new();
        collection.AddLogging();
        collection.AddSingleton<IDatabaseLocator>(new TempDirectoryDatabaseLocator(DataHost.NewDirectory()));

        collection.AddPangeaDbContext<NotesContext>(db =>
        {
            db.UseProvider(new UnqueuedSqliteProvider());
            db.Options.Migration = MigrationStrategy.EnsureCreated;
        });

        await using ServiceProvider services = collection.BuildServiceProvider();
        await DataHost.StartAsync(services, TestContext.Current.CancellationToken);

        IPangeaDbContext<NotesContext> data = services.GetRequiredService<IPangeaDbContext<NotesContext>>();

        for (int index = 0; index < 5; index++)
        {
            await data.WriteAsync((context, _) =>
            {
                context.Notes.Add(new Note { Title = $"note {index}" });
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);
        }

        Assert.Equal(5, await data.ReadAsync(
            (context, token) => context.Notes.CountAsync(token), TestContext.Current.CancellationToken));

        Assert.Equal("SQLite (unqueued)", (await services.GetRequiredService<IDatabaseMaintenance<NotesContext>>()
            .GetInfoAsync(TestContext.Current.CancellationToken)).ProviderName);
    }

    /// <summary>
    /// Pooling off, for a context that keeps state of its own between operations. The factory is a
    /// different one, so everything built on it is worth walking once.
    /// </summary>
    [Fact]
    public async Task WithoutPooling_EverythingStillWorks()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(
            db => db.Options.UsePooling = false, cancellationToken: TestContext.Current.CancellationToken);

        await database.Db.WriteAsync((context, _) =>
        {
            context.Notes.Add(new Note { Title = "unpooled" });
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.Equal("unpooled", await database.Db.ReadAsync(
            (context, token) => context.Notes.Select(note => note.Title).FirstAsync(token),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddDataSeeder_RegistersOneThatRunsAtStartup()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(
            configureServices: services => services.AddDataSeeder<NotesContext, CountingSeeder>(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, await database.Db.ReadAsync(
            (context, token) => context.Notes.CountAsync(token), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// An in-memory database is not a file, so the operations built on having one say so rather
    /// than failing somewhere deeper.
    /// </summary>
    [Fact]
    public async Task AnInMemoryDatabase_CannotBeBackedUpOrRestored()
    {
        ServiceCollection collection = new();
        collection.AddLogging();
        collection.AddSingleton<IDatabaseLocator>(new TempDirectoryDatabaseLocator(DataHost.NewDirectory()));

        collection.AddPangeaDbContext<NotesContext>(db =>
        {
            db.UseSqlite();
            db.Options.ConnectionString = "Data Source=:memory:";
            db.Options.Migration = MigrationStrategy.None;
        });

        await using ServiceProvider services = collection.BuildServiceProvider();

        IDatabaseMaintenance<NotesContext> maintenance =
            services.GetRequiredService<IDatabaseMaintenance<NotesContext>>();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => maintenance.BackupAsync(cancellationToken: TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<NotSupportedException>(() => new SqliteDbProvider()
            .RestoreAsync("Data Source=:memory:", "anywhere.bak", TestContext.Current.CancellationToken));

        DatabaseInfo info = await maintenance.GetInfoAsync(TestContext.Current.CancellationToken);

        Assert.Null(info.FilePath);
        Assert.Null(info.SizeBytes);
    }

    [Fact]
    public async Task AReadHonoursTheCancellationItIsGiven()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => database.Db.ReadAsync(
            (context, token) => context.Notes.CountAsync(token), cancelled.Token));
    }

    /// <summary>
    /// The command timeout is not decoration: it is what SQLite waits for a busy database, and it
    /// has to reach both the connection and the commands run over it.
    /// </summary>
    [Fact]
    public async Task TheCommandTimeoutReachesTheConnection()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(
            db => db.Options.CommandTimeout = TimeSpan.FromSeconds(7),
            cancellationToken: TestContext.Current.CancellationToken);

        await using NotesContext context = database.Db.Create();

        // DefaultTimeout, not ConnectionTimeout: opening a file needs no timeout, and this is the
        // one Microsoft.Data.Sqlite gives its commands - which is the one the busy retry loop uses.
        SqliteConnection connection = Assert.IsType<SqliteConnection>(context.Database.GetDbConnection());

        Assert.Equal(7, connection.DefaultTimeout);
    }
}
