using CdCSharp.Pangea.Data.Configuration;
using CdCSharp.Pangea.Data.Sqlite;
using CdCSharp.Pangea.Data.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CdCSharp.Pangea.Data.Tests;

/// <summary>
/// The settings a SQLite desktop application ends up at after being bitten, asserted here so they
/// stay set.
/// </summary>
public class SqliteDbProviderTests
{
    [Fact]
    public void TheConnectionStringTurnsOnWhatSqliteLeavesOff()
    {
        SqliteDbProvider provider = new();

        string connectionString = provider.ResolveConnectionString(
            new PangeaDbOptions { DatabaseFileName = "probe.db", CommandTimeout = TimeSpan.FromSeconds(45) },
            new TempDirectoryDatabaseLocator(DataHost.NewDirectory()));

        SqliteConnectionStringBuilder parsed = new(connectionString);

        Assert.True(parsed.ForeignKeys);
        Assert.True(parsed.Pooling);
        Assert.Equal(45, parsed.DefaultTimeout);
    }

    [Fact]
    public void AConnectionStringGivenByTheApplicationIsUsedAsWritten()
    {
        SqliteDbProvider provider = new();

        Assert.Equal(
            "Data Source=:memory:",
            provider.ResolveConnectionString(
                new PangeaDbOptions { ConnectionString = "Data Source=:memory:" },
                new TempDirectoryDatabaseLocator(DataHost.NewDirectory())));
    }

    [Fact]
    public void AnInMemoryDatabaseHasNoFile()
    {
        SqliteDbProvider provider = new();

        Assert.Null(provider.GetDatabaseFilePath("Data Source=:memory:"));
        Assert.Null(provider.GetDatabaseFilePath("Data Source=probe;Mode=Memory;Cache=Shared"));
        Assert.NotNull(provider.GetDatabaseFilePath("Data Source=probe.db"));
    }

    /// <summary>
    /// Write-ahead logging is what stops a read blocking behind a write. It is a property of the
    /// file, so setting it once at startup is enough - and asserting it means the day someone
    /// removes the PRAGMA, this says so rather than the users do.
    /// </summary>
    [Fact]
    public async Task StartupLeavesTheDatabaseInWalMode()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("wal", await ScalarAsync(database, "PRAGMA journal_mode;"));
    }

    [Fact]
    public async Task EveryConnectionEnforcesForeignKeys()
    {
        await using PangeaTestDatabase<NotesContext> database = await PangeaTestDatabase<NotesContext>.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("1", await ScalarAsync(database, "PRAGMA foreign_keys;"));
    }

    private static async Task<string?> ScalarAsync(PangeaTestDatabase<NotesContext> database, string sql)
    {
        await using NotesContext context = database.Db.Create();
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);

        try
        {
            await using SqliteCommand command = (SqliteCommand)context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;

            object? value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            return value?.ToString();
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
