using CdCSharp.Pangea.Data.Configuration;
using Microsoft.EntityFrameworkCore;

namespace CdCSharp.Pangea.Data.Abstractions;

/// <summary>
/// Everything the data feature needs a database engine to answer.
/// </summary>
/// <remarks>
/// <para>
/// One implementation per provider package, so an application installs the engine it uses and
/// nothing else: <c>CdCSharp.Pangea.Data</c> references no provider at all, and no application
/// carries a SQL Server driver to talk to a SQLite file.
/// </para>
/// <para>
/// A provider is registered by the <c>Use...</c> call in its own package, from inside the builder
/// handed to <c>AddPangeaDbContext</c>. That is also why the feature never has to look one up: the
/// application named it in the same statement that named the context.
/// </para>
/// </remarks>
public interface IPangeaDbProvider
{
    /// <summary>The engine's name, as it appears in <see cref="DatabaseInfo"/> and in errors.</summary>
    string Name { get; }

    /// <summary>
    /// Whether writes have to be taken one at a time. True for a single-file engine like SQLite,
    /// where two concurrent writers are not slower but broken: the second gets
    /// <c>database is locked</c>.
    /// </summary>
    bool SerializesWrites { get; }

    /// <summary>The connection string this database is reached by.</summary>
    string ResolveConnectionString(PangeaDbOptions options, IDatabaseLocator locator);

    /// <summary>Points <paramref name="builder"/> at this engine.</summary>
    void Configure(DbContextOptionsBuilder builder, string connectionString, PangeaDbOptions options);

    /// <summary>
    /// The file the database lives in, or <see langword="null"/> when it lives on a server. What
    /// backup, restore and the reported size are built on.
    /// </summary>
    string? GetDatabaseFilePath(string connectionString);

    /// <summary>
    /// Settings that belong to the database rather than to a connection, applied once at startup.
    /// Everything per-connection belongs in the connection string, where every connection gets it.
    /// </summary>
    Task PrepareAsync(DbContext context, CancellationToken cancellationToken);

    /// <summary>Writes a consistent copy of the database to <paramref name="targetPath"/>.</summary>
    /// <exception cref="NotSupportedException">The engine has no copy this feature can take.</exception>
    Task BackupAsync(DbContext context, string targetPath, CancellationToken cancellationToken);

    /// <summary>
    /// Puts <paramref name="backupPath"/> back. Called with no context open on the database.
    /// </summary>
    /// <exception cref="NotSupportedException">The engine has no restore this feature can perform.</exception>
    Task RestoreAsync(string connectionString, string backupPath, CancellationToken cancellationToken);

    /// <summary>Reclaims the space deleted rows left behind.</summary>
    Task CompactAsync(DbContext context, CancellationToken cancellationToken);
}
