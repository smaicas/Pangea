namespace CdCSharp.Pangea.Data.Configuration;

/// <summary>What to do about the database schema when the application starts.</summary>
public enum MigrationStrategy
{
    /// <summary>Nothing. The application takes care of its own schema.</summary>
    None,

    /// <summary>
    /// Create the schema from the model if the database does not exist yet, and leave it alone
    /// afterwards. No migration history, so the next model change needs the file deleted. For a
    /// cache or a scratch database, not for one holding anything the user would miss.
    /// </summary>
    EnsureCreated,

    /// <summary>Apply every pending migration.</summary>
    Migrate,

    /// <summary>
    /// Copy the database before applying pending migrations, and put the copy back if the
    /// migration fails. The default: a migration that half-ran on a user's machine is not
    /// something they can be talked through fixing.
    /// </summary>
    MigrateWithBackup
}

/// <summary>How one <see cref="Microsoft.EntityFrameworkCore.DbContext"/> is configured.</summary>
/// <remarks>
/// Set through the builder handed to <c>AddPangeaDbContext</c> rather than through
/// <c>services.Configure</c>, the way the rest of the toolkit's options are: an application can
/// register more than one context, and each has its own file, its own migration strategy and its
/// own provider.
/// </remarks>
public sealed class PangeaDbOptions
{
    /// <summary>
    /// The connection string, verbatim. Set this and the file name below is not consulted.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The database file, resolved against the per-platform data directory by the storage feature.
    /// </summary>
    public string DatabaseFileName { get; set; } = "app.db";

    /// <summary>
    /// A full path to the database file, overriding <see cref="DatabaseFileName"/> and the
    /// per-platform directory it would have gone in.
    /// </summary>
    public string? DatabasePath { get; set; }

    /// <summary>Where backups are written. Defaults to a <c>backups</c> folder beside the database.</summary>
    public string? BackupDirectory { get; set; }

    /// <summary>
    /// How many automatic backups to keep. Older ones are deleted as new ones are made.
    /// </summary>
    /// <remarks>
    /// Anything below one is treated as one. The backup a migration takes before it runs is the
    /// only copy of the database that exists while the migration is running, so pruning is never
    /// allowed to delete it.
    /// </remarks>
    public int BackupsToKeep { get; set; } = 3;

    public MigrationStrategy Migration { get; set; } = MigrationStrategy.MigrateWithBackup;

    /// <summary>
    /// Puts parameter values in the logs. Off, and worth leaving off outside a debug build: the
    /// parameters of a desktop application's queries are the user's own data.
    /// </summary>
    public bool SensitiveDataLogging { get; set; }

    public bool DetailedErrors { get; set; }

    /// <summary>
    /// Whether contexts are pooled. Pooling needs a constructor taking
    /// <c>DbContextOptions&lt;TContext&gt;</c>, which is the constructor a Pangea context has
    /// anyway. Turn it off for a context that keeps state of its own between operations.
    /// </summary>
    public bool UsePooling { get; set; } = true;

    public int MaxPoolSize { get; set; } = 32;

    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Where preparing this database sits among the application's startup initializers. Negative,
    /// so a database is ready before anything an application registers for itself.
    /// </summary>
    public int StartupOrder { get; set; } = -100;
}
