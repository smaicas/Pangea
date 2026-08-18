using CdCSharp.Pangea.Data.Abstractions;
using CdCSharp.Pangea.Data.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Data.Common;

namespace CdCSharp.Pangea.Data.Sqlite;

/// <summary>
/// SQLite: one file, no server, and the default answer for a desktop application that needs a
/// database at all.
/// </summary>
/// <remarks>
/// The defaults here are the ones a SQLite application ends up at after being bitten: write-ahead
/// logging so a reader and a writer stop blocking each other, a busy timeout so a contended write
/// waits instead of failing, foreign keys on because SQLite leaves them off, and writes taken one
/// at a time because the engine has exactly one writer whatever the callers think.
/// </remarks>
public sealed class SqliteDbProvider : IPangeaDbProvider
{
    private const string MemoryDataSource = ":memory:";

    private readonly Action<SqliteDbContextOptionsBuilder>? _configure;

    /// <remarks>
    /// Anything passed here is applied to EF's own SQLite options after this provider has set its
    /// defaults, for the application that needs one of them different.
    /// </remarks>
    public SqliteDbProvider(Action<SqliteDbContextOptionsBuilder>? configure = null) => _configure = configure;

    public string Name => "SQLite";

    /// <summary>
    /// Always. SQLite has one writer: two concurrent ones do not share the work, the second gets
    /// <c>SQLITE_BUSY</c>, and the application reports "database is locked" to a user who has no
    /// idea what that means.
    /// </summary>
    public bool SerializesWrites => true;

    public string ResolveConnectionString(PangeaDbOptions options, IDatabaseLocator locator)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(locator);

        if (!string.IsNullOrWhiteSpace(options.ConnectionString)) return options.ConnectionString;

        return new SqliteConnectionStringBuilder
        {
            DataSource = locator.GetDatabaseFilePath(options),

            // SQLite ignores foreign keys unless a connection asks for them, so this belongs in the
            // connection string where every connection gets it - not in a PRAGMA run once at
            // startup, which would leave every later connection without them.
            ForeignKeys = true,

            Pooling = true,

            // Microsoft.Data.Sqlite turns this into the busy timeout: how long a statement waits
            // for the writer ahead of it rather than failing.
            DefaultTimeout = (int)options.CommandTimeout.TotalSeconds
        }.ToString();
    }

    public void Configure(DbContextOptionsBuilder builder, string connectionString, PangeaDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        builder.UseSqlite(connectionString, sqlite =>
        {
            sqlite.CommandTimeout((int)options.CommandTimeout.TotalSeconds);
            _configure?.Invoke(sqlite);
        });
    }

    /// <summary>
    /// The file behind the connection string, or <see langword="null"/> for an in-memory database -
    /// which is not a file, has nothing to back up, and disappears with its connection.
    /// </summary>
    public string? GetDatabaseFilePath(string connectionString)
    {
        // A connection string that says nothing names no file, which is the answer this method
        // documents. Throwing here would fail the startup path that asks the question in order to
        // find out whether there is a file to prepare at all.
        if (string.IsNullOrWhiteSpace(connectionString)) return null;

        SqliteConnectionStringBuilder builder = new(connectionString);

        if (builder.Mode is SqliteOpenMode.Memory) return null;

        string source = builder.DataSource;

        if (string.IsNullOrWhiteSpace(source)) return null;
        if (string.Equals(source, MemoryDataSource, StringComparison.OrdinalIgnoreCase)) return null;

        return Path.GetFullPath(source);
    }

    /// <summary>
    /// Turns on write-ahead logging, which is a property of the database file rather than of a
    /// connection: set once, it stays set, and every later connection opens a WAL database.
    /// </summary>
    public async Task PrepareAsync(DbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // An in-memory database has no file to journal to, and asking for WAL on one is refused.
        if (GetDatabaseFilePath(context.Database.GetConnectionString() ?? string.Empty) is null) return;

        await ExecuteAsync(context, "PRAGMA journal_mode=WAL;", cancellationToken);
    }

    /// <summary>
    /// Takes the copy with <c>VACUUM INTO</c> rather than by copying the file.
    /// </summary>
    /// <remarks>
    /// A file copy of a live SQLite database is not a database: the pages it needs may still be in
    /// the write-ahead log, and the copy is a torn one. <c>VACUUM INTO</c> is taken by the engine,
    /// is consistent by construction, and comes out compacted.
    /// </remarks>
    public async Task BackupAsync(DbContext context, string targetPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        if (GetDatabaseFilePath(context.Database.GetConnectionString() ?? string.Empty) is null)
        {
            throw new NotSupportedException(
                "An in-memory SQLite database has nothing to back up: it exists only while a connection to it is open.");
        }

        string full = Path.GetFullPath(targetPath);
        string? directory = Path.GetDirectoryName(full);

        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        // VACUUM INTO refuses to overwrite, so the caller's intent to replace is carried out here.
        if (File.Exists(full)) File.Delete(full);

        await ExecuteAsync(context, "VACUUM INTO $target;", cancellationToken, ("$target", full));
    }

    /// <summary>
    /// Puts a backup back in place of the database file.
    /// </summary>
    /// <remarks>
    /// The connection pool is emptied first. Microsoft.Data.Sqlite keeps connections open behind
    /// it, and on Windows an open handle is enough to make replacing the file fail - on the user's
    /// machine, in the middle of recovering from a failed migration.
    /// </remarks>
    public Task RestoreAsync(string connectionString, string backupPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);

        string target = GetDatabaseFilePath(connectionString) ?? throw new NotSupportedException(
            "An in-memory SQLite database cannot be restored: there is no file to replace.");

        cancellationToken.ThrowIfCancellationRequested();

        SqliteConnection.ClearAllPools();

        // Before the copy, not after: a write-ahead log left over from the old database would be
        // replayed onto the new one, which is how a restore turns into a corruption.
        DeleteIfPresent(target + "-wal");
        DeleteIfPresent(target + "-shm");

        File.Copy(backupPath, target, overwrite: true);

        return Task.CompletedTask;
    }

    public Task CompactAsync(DbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ExecuteAsync(context, "VACUUM;", cancellationToken);
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>
    /// Runs a statement on the context's own connection.
    /// </summary>
    /// <remarks>
    /// Through ADO.NET rather than <c>ExecuteSqlRaw</c>: PRAGMA and VACUUM cannot run inside a
    /// transaction, and going around EF's command pipeline is the way to be sure none is opened
    /// for them.
    /// </remarks>
    private static async Task ExecuteAsync(
        DbContext context,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        bool opened = false;

        if (context.Database.GetDbConnection().State is not System.Data.ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
            opened = true;
        }

        try
        {
            DbConnection connection = context.Database.GetDbConnection();

            await using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            foreach ((string name, object value) in parameters)
            {
                DbParameter parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (opened) await context.Database.CloseConnectionAsync();
        }
    }
}
