using CdCSharp.Pangea.Data.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CdCSharp.Pangea.Data.Sqlite;

/// <summary>Chooses SQLite for a context being registered.</summary>
public static class PangeaSqliteBuilderExtensions
{
    /// <summary>
    /// Puts the context on a SQLite file in the application's per-platform data directory.
    /// </summary>
    /// <remarks>
    /// <code>
    /// services.AddPangeaDbContext&lt;AppDbContext&gt;(db => db.UseSqlite("orders.db"));
    /// </code>
    /// </remarks>
    public static PangeaDbBuilder UseSqlite(
        this PangeaDbBuilder builder,
        string? databaseFileName = null,
        Action<SqliteDbContextOptionsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!string.IsNullOrWhiteSpace(databaseFileName)) builder.Options.DatabaseFileName = databaseFileName;

        return builder.UseProvider(new SqliteDbProvider(configure));
    }
}
