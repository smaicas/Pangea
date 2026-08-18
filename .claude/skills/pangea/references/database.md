# Database

Entity Framework Core for a desktop application. Read this before writing any data access in a
Pangea application.

The feature is **not** in the `CdCSharp.Pangea` package. The application installs the engine it
uses:

```bash
dotnet add package CdCSharp.Pangea.Data.Sqlite   # brings CdCSharp.Pangea.Data with it
```

---

## The three rules

1. **Never inject `DbContext`.** Inject `IPangeaDbContext<TContext>`. Registering a context removes
   EF's own registration of it, so injecting one fails at startup rather than leaking.
2. **Never call `SaveChangesAsync` yourself** inside `WriteAsync` — it is called for you.
3. **Never scaffold the schema at runtime with `EnsureCreated`** for data the user would miss. Write
   a migration; the application applies it at startup.

---

## Context and entity

An ordinary `DbContext` with the constructor the factory uses. Nothing else.

```csharp
using Microsoft.EntityFrameworkCore;

namespace DataSample;

public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Note>(note =>
        {
            note.HasKey(entity => entity.Id);
            note.Property(entity => entity.Title).IsRequired().HasMaxLength(80);
            note.HasIndex(entity => entity.CreatedUtc);
        });
    }
}
```

Do **not** override `OnConfiguring`: where the file lives and how it is opened were decided in
`App.Configure`, and a context that configures itself ignores all of it.

---

## Registration

In `App.Configure`, beside the application's other services:

```csharp
using CdCSharp.Pangea.Data;
using CdCSharp.Pangea.Data.Configuration;
using CdCSharp.Pangea.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace DataSample;

public static class DataRegistration
{
    public static void Register(IServiceCollection services) =>
        services.AddPangeaDbContext<AppDbContext>(db =>
        {
            db.UseSqlite("app.db");                                    // engine, and the file name

            db.Options.Migration = MigrationStrategy.MigrateWithBackup; // the default
            db.Options.BackupsToKeep = 3;
            db.Options.SensitiveDataLogging = false;                    // parameters are user data
        });
}
```

The file goes in the per-platform data directory the storage feature resolves. Set
`db.Options.DatabasePath` only when the application genuinely needs a path of its own.

Forgetting `UseSqlite()` throws at startup with the package name in the message.

---

## Using it from a view model

```csharp
using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace DataSample;

public partial class NotesViewModel : ViewModelBase
{
    private readonly IPangeaDbContext<AppDbContext> _db;

    [Binding] private ObservableCollection<Note> _notes = [];
    [Binding] private string _newTitle = "";

    public NotesViewModel(IServiceProvider services) : base(services)
    {
        _db = services.GetRequiredService<IPangeaDbContext<AppDbContext>>();

        // A constructor cannot await. The window is better up with an empty list for a moment.
        _ = LoadAsync();
    }

    public RelayCommand AddCommand => CreateCommand(AddAsync, () => !string.IsNullOrWhiteSpace(NewTitle));

    // Built on the UI thread, ready to bind to.
    public async Task LoadAsync() =>
        Notes = await _db.ToObservableAsync(context => context.Notes.OrderByDescending(note => note.CreatedUtc));

    private async Task AddAsync()
    {
        string title = NewTitle.Trim();

        // Runs the change and saves it. Writes are taken one at a time: SQLite has one writer.
        await _db.WriteAsync((context, token) =>
        {
            context.Notes.Add(new Note { Title = title });
            return Task.CompletedTask;
        });

        NewTitle = "";
        await LoadAsync();
    }

    public Task<int> CountAsync() =>
        _db.ReadAsync((context, token) => context.Notes.CountAsync(token));
}
```

| Method | What it does |
|---|---|
| `ReadAsync` | A query, tracking off. What comes back is data, not live entities |
| `WriteAsync` | A change plus `SaveChangesAsync`, one write at a time. Returns rows written |
| `ToObservableAsync` | A query whose collection is built on the UI thread |
| `Create()` | A context of your own, to dispose — for a transaction or a bulk load |

Each call builds a context from the pooled factory and disposes it before returning. Do not hold
one: a `DbContext` is a unit of work, not a service, and one kept by a view model tracks everything
it ever loaded and serves stale values.

---

## Migrations

The application applies its own migrations at startup, behind the splash window, because nobody is
going to run `dotnet ef database update` on the user's machine.

| `MigrationStrategy` | When |
|---|---|
| `MigrateWithBackup` | **Default.** Copies the database first and puts it back if the migration fails |
| `Migrate` | Migrations, no backup |
| `EnsureCreated` | Schema from the model, no history. Caches and scratch databases only |
| `None` | The application looks after its own schema |

Writing one needs the design-time factory the `pangea-data` template ships, or the tooling starts
the Avalonia application looking for a context:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DataSample;

public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    // Only used to work out the SQL dialect. Nothing is written to this file.
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
```

```bash
dotnet ef migrations add AddSomething --output-dir Data/Migrations
```

There is no `database update` step.

---

## Seeding

Runs at startup after the schema is up to date, in `Order`, on **every** run — so check first.
Pending changes are saved for you.

```csharp
using CdCSharp.Pangea.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace DataSample;

public sealed class WelcomeNoteSeeder : IDataSeeder<AppDbContext>
{
    public async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Notes.AnyAsync(cancellationToken)) return;

        context.Notes.Add(new Note { Title = "Welcome" });
    }
}
```

```csharp
using CdCSharp.Pangea.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DataSample;

public static class SeederRegistration
{
    public static void Register(IServiceCollection services) =>
        services.AddDataSeeder<AppDbContext, WelcomeNoteSeeder>();
}
```

---

## Backup, restore, size

A desktop application has no administrator behind it, so this belongs on a settings screen.

```csharp
using CdCSharp.Pangea.Data.Abstractions;

namespace DataSample;

public sealed class DatabasePanel
{
    private readonly IDatabaseMaintenance<AppDbContext> _maintenance;

    public DatabasePanel(IDatabaseMaintenance<AppDbContext> maintenance) => _maintenance = maintenance;

    public async Task<string> DescribeAsync()
    {
        DatabaseInfo info = await _maintenance.GetInfoAsync();

        return $"{info.ProviderName} · {info.SizeBytes} bytes · " +
               $"{info.AppliedMigrations.Count} applied · {info.PendingMigrations.Count} pending";
    }

    public Task<string> BackUpAsync() => _maintenance.BackupAsync();

    public Task CompactAsync() => _maintenance.CompactAsync();

    // Nothing may be querying: this replaces the file underneath open connections.
    public Task RestoreAsync(string backupPath) => _maintenance.RestoreAsync(backupPath);
}
```

SQLite backups are taken with `VACUUM INTO`, so they are consistent and compacted. Automatic ones
are pruned to `BackupsToKeep`.

---

## Testing

`CdCSharp.Pangea.Data.Testing` gives a real SQLite database in a temporary directory, registered
through the same path the application uses and deleted with the test.

```csharp
using CdCSharp.Pangea.Data.Testing;
using Microsoft.EntityFrameworkCore;

namespace DataSample;

public static class DatabaseTestExample
{
    public static async Task<int> RoundTripAsync()
    {
        await using PangeaTestDatabase<AppDbContext> database =
            await PangeaTestDatabase<AppDbContext>.CreateAsync();

        await database.Db.WriteAsync((context, token) =>
        {
            context.Notes.Add(new Note { Title = "first" });
            return Task.CompletedTask;
        });

        return await database.Db.ReadAsync((context, token) => context.Notes.CountAsync(token));
    }
}
```

Contexts registered this way default to `EnsureCreated`, because a context written for a test
usually has no migrations. Pass `db.Options.Migration = MigrationStrategy.Migrate` to exercise the
real ones.

---

## Pitfalls

The first three are reported by the analyzer the feature ships, as build warnings. Do not suppress
them; fix them.

- **`PGD001` — registering with no engine.** `AddPangeaDbContext` whose callback never calls
  `UseSqlite()`. The container throws when it is built.
- **`PGD002` — injecting `AppDbContext`.** Resolve `IPangeaDbContext<AppDbContext>` instead. The
  context is deliberately not registered. (A context registered with EF's own `AddDbContext` is
  the application's own business and is not reported.)
- **`PGD003` — `SaveChangesAsync` inside `WriteAsync`.** `WriteAsync` saves once the callback
  returns; the second save is noise.
- **`OnConfiguring` in the context** — throws away everything `AddPangeaDbContext` decided.
- **Building an `ObservableCollection` on a background thread and binding it** — use
  `ToObservableAsync`, which builds it on the UI thread.
- **`EnsureCreated` on a real database** — it writes no migration history, so the next model change
  needs the user's file deleted.
- **Trimming** — EF Core is not trim-safe. `TrimMode=full` builds and then throws
  `MissingMethodException` on the first query. Root the EF assemblies, or do not trim.
