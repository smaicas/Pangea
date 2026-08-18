# 🌍 CdCSharp.Pangea

<div align="center">

[![NuGet Version](https://img.shields.io/nuget/v/CdCSharp.Pangea?style=flat-square&logo=nuget&logoColor=white&label=NuGet&color=004880)](https://www.nuget.org/packages/CdCSharp.Pangea)
[![NuGet Downloads](https://img.shields.io/nuget/dt/CdCSharp.Pangea?style=flat-square&logo=nuget&logoColor=white&label=Downloads&color=004880)](https://www.nuget.org/packages/CdCSharp.Pangea)
[![Build Status](https://img.shields.io/github/actions/workflow/status/smaicas/CdCSharp.Pangea/.github/workflows/nuget-publish.yml?style=flat-square&logo=github&label=Build)](https://github.com/smaicas/CdCSharp.Pangea/actions)
[![License](https://img.shields.io/github/license/smaicas/CdCSharp.Pangea?style=flat-square&logo=opensourceinitiative&logoColor=white&label=License&color=green)](https://github.com/smaicas/CdCSharp.Pangea/blob/main/LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-12.1.1-663399?style=flat-square&logo=avalonia)](https://avaloniaui.net/)

**An Avalonia toolkit: MVVM with generated bindings, themes as C# classes, storage and localization**

[📦 Installation](#-installation) • [🚀 Quick start](#-quick-start) • [🧠 Binding](#-binding) • [🎨 Theming](#-theming) • [💾 Storage](#-storage) • [🗄️ Database](#-database) • [🌐 Localization](#-localization) • [🧭 Navigation](#-navigation) • [💬 Dialogs](#-dialogs) • [🧪 Testing](#-testing-your-own-application) • [🤖 AI agents](#-ai-coding-agents)

</div>

> **Status: pre-1.0.** The API is still moving and breaking changes land without a deprecation
> cycle. Pin a version if you depend on it.

---

## 🎯 What is Pangea?

Pangea wires up the parts an Avalonia application usually assembles by hand: a DI container, a
source generator that turns fields into observable properties, a theme you can restyle from C#,
per-platform storage paths, and localization.

Each capability is a **feature** — a class implementing `IPangeaFeature`, discovered at startup,
registering its own services.

---

## 📦 Installation

### Start from a template

```bash
dotnet new install CdCSharp.Pangea.Templates
```

Three starting points. `pangea-app` is the smallest one: the startup wiring, a window, and a sample
view model and palette showing the toolkit's conventions.

```bash
dotnet new pangea-app -n MyApp
cd MyApp && dotnet run
```

| Option | Default | |
|---|---|---|
| `--IncludeSkill` | `true` | Copy the [agent skill](#-ai-coding-agents) into the project |
| `--Sample` | `true` | Include the sample view model and palette |
| `--PangeaVersion` | matches the template | Version of the Pangea packages to reference |

`pangea-shell` is the worked example. It is an application with a menu, three screens and every
feature wired: navigation with a typed request, strings localized into two cultures, settings saved
to the per-platform data directory and restored on the next run by an application's own feature,
validation rules on a form, a confirmation dialog refusing to leave a screen with unsaved changes,
and a custom theme.

```bash
dotnet new pangea-shell -n MyApp
cd MyApp && dotnet run
```

| Option | Default | |
|---|---|---|
| `--IncludeSkill` | `true` | Copy the [agent skill](#-ai-coding-agents) into the project |
| `--PangeaVersion` | matches the template | Version of the Pangea packages to reference |

`pangea-data` is the same idea for the [database feature](#-database), which the shell template
deliberately leaves out — it keeps its settings in a JSON file, and one application showing two ways
to store things teaches neither. This one is a list backed by SQLite: a context and a migration, the
migration applied at startup behind a splash window with a backup taken first, a seeder, and backup
and compact offered to the user.

```bash
dotnet new pangea-data -n MyApp
cd MyApp && dotnet run
```

| Option | Default | |
|---|---|---|
| `--IncludeSkill` | `true` | Copy the [agent skill](#-ai-coding-agents) into the project |
| `--PangeaVersion` | matches the template | Version of the Pangea packages to reference |

Start from `pangea-shell` when you want to see how a feature is meant to be used, `pangea-data` when
the application has a database in it, and `pangea-app` when you want an empty room.

### Add to an existing project

```bash
dotnet add package CdCSharp.Pangea
```

That is the package to install: besides pulling in every feature, it is where the application model
lives — `PangeaApplication`, `UsePangea()` and the window manager.

Installing it also puts the whole build-time toolchain into the project, with nothing to configure:

| | |
|---|---|
| `[Binding]` source generator | Fields become observable properties, with `PGB001`–`PGB006` when they cannot |
| Startup catalog generator | Replaces the assembly scan at startup — see [What startup does instead of scanning](#what-startup-does-instead-of-scanning) |
| Localization analyzer | `PGL001`/`PGL002` on resource keys — see [Keys checked at compile time](#keys-checked-at-compile-time) |

The database feature brings one more, `PGD001`–`PGD003`, but only to a project that installs it —
see [Checked at compile time](#checked-at-compile-time).

Each one also travels with the feature package it belongs to, so a project that installs only
`CdCSharp.Pangea.Binding` still gets the generator that makes `[Binding]` mean anything.

The features are also published on their own, for using a piece of the toolkit as a plain library
without the Pangea application model. Each depends only on `CdCSharp.Pangea.Core`:

```bash
dotnet add package CdCSharp.Pangea.Binding       # [Binding] attribute + source generator
dotnet add package CdCSharp.Pangea.Theming       # palettes, themes, theme service
dotnet add package CdCSharp.Pangea.Storage       # per-platform paths and file access
dotnet add package CdCSharp.Pangea.Localization  # cultures and resource strings
dotnet add package CdCSharp.Pangea.Navigation    # typed navigation requests and a host
```

The database feature is the one thing the meta-package does **not** pull in, because EF Core and a
native SQLite driver are megabytes an application that never queries anything should not carry:

```bash
dotnet add package CdCSharp.Pangea.Data.Sqlite   # EF Core on SQLite, and CdCSharp.Pangea.Data with it
```

And, for the test project rather than the application:

```bash
dotnet add package CdCSharp.Pangea.Testing       # test doubles: see Testing your own application
dotnet add package CdCSharp.Pangea.Data.Testing  # a real SQLite database, deleted with the test
```

Targets **.NET 10** and **Avalonia 12.1**.

---

## 🚀 Quick start

```csharp
// App.axaml.cs
public partial class App : PangeaApplication
{
    public override void Configure(IServiceCollection services)
    {
        services.AddSingleton<IDataService, DataService>();
        // View models deriving from ViewModelBase are registered automatically
    }
}

// Program.cs
public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace()
        .UsePangea();
```

`UsePangea()` scans the application's assemblies once, registers every feature it finds, registers
your view models, and builds the container. `PangeaApplication` then lets each feature configure the
running application and shows the main window.

---

## 🧠 Binding

Fields marked `[Binding]` become properties that raise change notifications — including for the
computed properties and commands that depend on them.

```csharp
public partial class ProductViewModel : ViewModelBase
{
    public ProductViewModel(IServiceProvider services) : base(services) { }

    [Binding] private string _name = "";
    [Binding] private decimal _price;
    [Binding] private bool _isAvailable = true;
    [Binding(ReadOnly = true)] private string _sku = "";
    [Binding(PropertyName = "Tags")] private ObservableCollection<string> _categories = [];

    public string DisplayName => $"{Name} ({Price:C})";
    public bool CanOrder => IsAvailable && Price > 0;

    public RelayCommand OrderCommand => CreateCommand(Order, () => CanOrder);
    public RelayCommand<string> AddTagCommand => CreateCommand<string>(AddTag);

    private void Order() => IsAvailable = false;

    private void AddTag(string? tag)
    {
        if (!string.IsNullOrEmpty(tag)) Tags.Add(tag);
    }

    // Optional hook, called by the generated setter
    partial void OnNameChanged() { }
}
```

The generator analyses the class and emits only the notifications that are actually needed:

```csharp
partial class ProductViewModel
{
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnNameChanged();

                // Computed property notifications
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public decimal Price
    {
        get => _price;
        set
        {
            if (SetProperty(ref _price, value))
            {
                OnPriceChanged();

                // Computed property notifications
                OnPropertyChanged(nameof(CanOrder));
                OnPropertyChanged(nameof(DisplayName));

                // Command CanExecute notifications
                OrderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    // ReadOnly: getter only, no change hook, no notifications
    public string Sku
    {
        get => _sku;
    }

    partial void OnNameChanged();
    partial void OnPriceChanged();
}
```

**What it works out for you**

| | |
|---|---|
| Computed properties | An expression- or getter-bodied property that reads a binding property is notified when it changes |
| Transitive chains | `Price → DisplayName → Summary` propagates without you listing it |
| Commands | A command whose `CanExecute` reads a property — directly, through a `CanX` method, or through a computed property — gets `RaiseCanExecuteChanged()` |
| Collections | If a change hook fills a collection - in its own body or through anything it calls - whatever reads that collection is notified, and only that collection |

**When it cannot generate**, it says so instead of letting the compiler complain about a file you
did not write: a class that is not `partial` (`PGB001`), one whose base supplies no change
notification (`PGB002`), two fields that would produce the same property (`PGB003`), a name the
class already declares (`PGB004`), `[Binding]` on a `static` field (`PGB005`), or a generated
property that hides a base member (`PGB006`). The last two are warnings. A class
with an error generates nothing, so the symptom is a property that is not there.

### Validation

Rules go on the field. The generator copies them onto the generated property and validates on every
set, through `INotifyDataErrorInfo` — which Avalonia already listens to.

```csharp
public partial class SignUpViewModel : ViewModelBase
{
    [Binding]
    [Required(ErrorMessage = "An email is required.")]
    [EmailAddress] private string _email = "";

    [Binding, Range(18, 120)] private int _age;

    // HasErrors comes from ViewModelBase and moves as the user types
    public RelayCommand SignUpCommand => CreateCommand(SignUp, () => !HasErrors);
}
```

```xml
<!-- nothing to wire: the control decorates itself -->
<TextBox Text="{Binding Email}" />
```

Any `ValidationAttribute` works, including your own — the rules are evaluated by
`System.ComponentModel.DataAnnotations`, not reimplemented in generated code. A property is
validated when it is set, so an untouched form shows nothing; `ValidateAll()` checks everything and
tells you whether the view model is valid, which is what a Save button asks first.

### Commands

`CreateCommand` builds a `RelayCommand` bound to the UI dispatcher.

```csharp
public RelayCommand SaveCommand => CreateCommand(Save, () => CanSave);          // sync
public RelayCommand LoadCommand => CreateCommand(LoadAsync);                    // async
public RelayCommand<Item> RemoveCommand => CreateCommand<Item>(Remove);         // parameterised
```

A **synchronous body runs on the UI thread**, marshalled if you invoke the command from elsewhere.
Background work belongs in the `Func<Task>` overload, where leaving the UI thread is explicit.

Failures always reach `ViewModelBase.OnCommandError`, which you can override. `ExecuteAsync` also
rethrows so an awaiting caller can react; `ICommand.Execute` cannot, and does not.

---

## 🎨 Theming

A **theme** is a pair of palettes, light and dark. A **variant** is which of the two is showing.
They are separate axes: switching theme keeps the variant, and vice versa.

### Restyling the application

Override the colours you care about — everything derived from them follows:

```csharp
using CdCSharp.Pangea.Theming.Palettes;

public sealed class BrandDark : DarkPalette
{
    public override Color ThemeAccentColor => Color.Parse("#FF4F9DDE");
    public override Color ThemeBackgroundColor => Color.Parse("#FF101418");
}

public sealed class BrandLight : PangeaPalette   // the base is the light palette
{
    public override Color ThemeAccentColor => Color.Parse("#FF1B6EC2");
}
```

```csharp
services.Configure<ThemingOptions>(options =>
{
    // Replace the toolkit's own theme...
    options.Themes[PangeaTheme.DefaultName] = new PangeaTheme(new BrandLight(), new BrandDark());

    // ...or add more and let the user choose
    options.Themes["Contrast"] = new PangeaTheme(new ContrastLight(), new ContrastDark());
    options.DefaultTheme = "Contrast";

    options.EnableSystemThemeDetection = true;      // follow the OS preference
    options.FallbackVariant = ThemeVariant.Dark;    // when it has none
});
```

Every colour property name **is** its resource key, and each one also produces a brush with `Color`
swapped for `Brush`. Overriding `ThemeAccentColor` therefore updates `ThemeAccentColor`,
`ThemeAccentBrush`, and everything derived from them.

### Switching at runtime

```csharp
public class AppearanceViewModel : ViewModelBase
{
    private readonly IThemeService _themes;

    public AppearanceViewModel(IServiceProvider services) : base(services) =>
        _themes = services.GetRequiredService<IThemeService>();

    public IReadOnlyCollection<string> Themes => _themes.AvailableThemes;

    public RelayCommand<string> UseTheme => CreateCommand<string>(name => _themes.SetTheme(name!));
    public RelayCommand UseDark => CreateCommand(() => _themes.SetVariant(ThemeVariant.Dark));
}
```

The toolkit ships a `ThemeSelector` control with a view model that drives both axes.

Because the palettes are Avalonia theme variants, a `ThemeVariantScope` can render part of the UI in
the opposite variant to the rest of the application.

### Control themes

The control dictionaries under `Resources/Controls/Shared` are Avalonia's Simple theme, vendored as
a starting point. A manifest records which files are still untouched copies and which this repo has
taken ownership of, and a test fails when the two drift apart or when the Avalonia version moves
without the theme being re-vendored.

---

## 💾 Storage

Per-platform application folders plus file access.

```csharp
public class SettingsService(IStorageService storage)
{
    private readonly string _path = storage.GetDataFilePath("settings.json");

    public Task<Settings?> LoadAsync() => storage.ReadJsonAsync<Settings>(_path);

    public Task SaveAsync(Settings settings) => storage.WriteJsonAsync(_path, settings);
}
```

```csharp
services.Configure<StorageOptions>(options =>
{
    options.ApplicationName = "MyApp";   // folder name under the platform's data directory
    options.UsePortableMode = false;     // true keeps everything next to the executable
    options.CustomDataPath = null;       // or somewhere of your choosing
});
```

Writes create the folders they need. Reads do not: `ReadTextAsync` fails on a missing file the way
`File.ReadAllTextAsync` does, while `ReadJsonAsync` returns null, for state that may not exist yet.

---

## 🗄️ Database

Entity Framework Core, wired the way a desktop application needs it rather than the way a web
request does. It is not part of the `CdCSharp.Pangea` package: EF Core plus a native SQLite driver
is several megabytes that an application storing its state in a JSON file should not carry. The
engine comes with its provider package.

```bash
dotnet add package CdCSharp.Pangea.Data.Sqlite   # brings CdCSharp.Pangea.Data with it
```

Register a context in `App.Configure`, beside everything else the application registers:

```csharp
services.AddPangeaDbContext<AppDbContext>(db =>
{
    db.UseSqlite("notes.db");                                 // the provider, and the file name
    db.Options.Migration = MigrationStrategy.MigrateWithBackup;
});
```

The file goes in the folder the [storage feature](#-storage) keeps this application's data in —
`%APPDATA%` on Windows, `~/.config` on Linux, `~/Library/Application Support` on macOS, or beside
the executable in portable mode. That is the reason this feature exists rather than a bare
`UseSqlite` call.

The context is an ordinary `DbContext` with the constructor the factory uses:

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();
}
```

### Reaching it from a view model

Ask for `IPangeaDbContext<AppDbContext>`, never for the context itself:

```csharp
public partial class NotesViewModel : ViewModelBase
{
    private readonly IPangeaDbContext<AppDbContext> _db;

    [Binding] private ObservableCollection<Note> _notes = [];

    public NotesViewModel(IServiceProvider services) : base(services) =>
        _db = services.GetRequiredService<IPangeaDbContext<AppDbContext>>();

    public async Task LoadAsync() =>
        Notes = await _db.ToObservableAsync(context => context.Notes.OrderBy(note => note.Title));

    public Task AddAsync(string title) => _db.WriteAsync((context, token) =>
    {
        context.Notes.Add(new Note { Title = title });
        return Task.CompletedTask;
    });
}
```

Each call builds a context from the pooled factory, uses it and disposes it. A `DbContext` is a
unit of work: it is not thread-safe, and it remembers every entity it has loaded. Both are fine for
a request that lives for milliseconds and wrong for a window that stays open all day — a context
injected into a view model and kept would grow without bound and serve values that changed hours
ago. Registering a context therefore *removes* EF's own registration of it, so injecting one is a
startup error rather than a leak nobody notices.

| | |
|---|---|
| `ReadAsync` | Runs a query with tracking off — what comes back is data for the UI |
| `WriteAsync` | Runs a change and saves it, one write at a time |
| `ToObservableAsync` | A query whose result is built on the UI thread, ready to bind to |
| `Create()` | A context of your own, for a transaction or a bulk load |

### Migrations on the user's machine

Nobody is going to run `dotnet ef database update` on a machine you do not have. So pending
migrations are applied at startup, which makes it the one part of startup that can destroy
something — hence the default:

| `MigrationStrategy` | |
|---|---|
| `MigrateWithBackup` | **Default.** Copies the database, migrates, and puts the copy back if the migration fails |
| `Migrate` | Applies pending migrations |
| `EnsureCreated` | Creates the schema from the model if there is no database yet. No history, so the next model change needs the file deleted |
| `None` | The application looks after its own schema |

The work runs behind a splash window rather than on the UI thread — see
[Work that has to finish first](#work-that-has-to-finish-first). If it fails, the splash says why
and the main window never opens: an application whose database did not open has nothing to show.

Writing a migration is the ordinary EF workflow. What the template adds is an
`IDesignTimeDbContextFactory`, without which the tooling starts the Avalonia application looking
for a context:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add AddSomething --output-dir Data/Migrations
```

There is no `database update` step. The application applies its own migrations.

### Backup, restore and size

There is no database administrator behind a desktop application, so whatever the user will be told
to do has to be a button in the application:

```csharp
public class SettingsViewModel(IDatabaseMaintenance<AppDbContext> maintenance)
{
    public async Task ShowAsync()
    {
        DatabaseInfo info = await maintenance.GetInfoAsync();
        // info.FilePath, info.SizeBytes, info.AppliedMigrations, info.PendingMigrations
    }

    public Task<string> BackUpAsync() => maintenance.BackupAsync();

    public Task CompactAsync() => maintenance.CompactAsync();
}
```

SQLite backups are taken with `VACUUM INTO`, not by copying the file: a copy of a live database may
be missing pages that are still in the write-ahead log. Automatic backups are pruned to
`BackupsToKeep`.

### Seeding

```csharp
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
services.AddDataSeeder<AppDbContext, WelcomeNoteSeeder>();
```

Seeders run at startup after the schema is up to date, in `Order`, on **every** run — so the check
above is not optional. Whatever is left pending on the context is saved for you.

### What the SQLite provider sets, and why

Write-ahead logging, so a read does not block behind a write. A busy timeout, so a contended write
waits instead of failing. Foreign keys on, because SQLite leaves them off. And writes taken one at
a time, because the engine has exactly one writer whatever the callers think — without that, the
second concurrent save reports "database is locked" to a user who has no idea what that means.

### Checked at compile time

Installing the feature brings an analyzer with it. Three rules, all warnings, all for mistakes that
compile and then surface much later than they were made:

| | |
|---|---|
| `PGD001` | `AddPangeaDbContext` with no `UseSqlite()` — the container throws when it is built, naming a call that is nowhere near the registration |
| `PGD002` | `GetRequiredService<AppDbContext>()` — the context is deliberately not registered, and asking for one is the leak this feature exists to prevent. A context the application registered itself with EF's own `AddDbContext` is left alone |
| `PGD003` | `SaveChangesAsync` inside `WriteAsync`, which saves for you. Harmless and invisible: the second save finds nothing to do |

Turn one down in a `.globalconfig` where it does not suit the project.

### Trimming

EF Core is not trim-safe and says so: its API carries `[RequiresUnreferencedCode]` and
`[RequiresDynamicCode]`. Published with `TrimMode=full` and no compiled model, a SQLite application
builds and then throws `MissingMethodException` on the first query. Nothing else in Pangea depends
on these packages, so the rest of the toolkit is unaffected.

If you need to trim, the verified recipe is to keep EF out of it:

```xml
<TrimMode>partial</TrimMode>
<ItemGroup>
  <TrimmerRootAssembly Include="Microsoft.EntityFrameworkCore" />
  <TrimmerRootAssembly Include="Microsoft.EntityFrameworkCore.Relational" />
  <TrimmerRootAssembly Include="Microsoft.EntityFrameworkCore.Sqlite" />
</ItemGroup>
```

EF's compiled models make full trimming work too, but they are not wired in here on purpose: a
compiled model that was not regenerated after a model change does not complain — it writes the
wrong columns. NativeAOT is out regardless, because building the design-time model a migration
needs is not something it supports.

### Testing

```bash
dotnet add package CdCSharp.Pangea.Data.Testing
```

A real SQLite database in a directory of its own, registered through the same
`AddPangeaDbContext` path the application uses, and deleted with the test:

```csharp
await using PangeaTestDatabase<AppDbContext> database = await PangeaTestDatabase<AppDbContext>.CreateAsync();

await database.Db.WriteAsync((context, token) =>
{
    context.Notes.Add(new Note { Title = "first" });
    return Task.CompletedTask;
});

Assert.Equal(1, await database.Db.ReadAsync((context, token) => context.Notes.CountAsync(token)));
```

---

## 🌐 Localization

```csharp
services.Configure<LocalizationOptions>(options =>
{
    options.SupportedCultures = ["en-US", "es-ES"];
    options.DefaultCulture = "en-US";
    options.AutoDetectCulture = true;

    // Assemblies holding the .resx-generated resource classes
    options.ResourceAssemblies.Add(typeof(Strings).Assembly);
});
```

```csharp
public class GreetingViewModel : ViewModelBase
{
    private readonly ILocalizationService _localization;

    public string Welcome => _localization.GetString("WelcomeMessage");

    public void SwitchToSpanish() => _localization.SetCulture("es-ES");
}
```

An unresolved key comes back as itself, so a missing translation is visible rather than blank.
`SetCulture` applies to the whole application, including threads started afterwards, and raises
`CultureChanged`.

### Changing language while the application runs

`SetCulture` changes the culture, but nothing on screen re-reads its text unless something tells it
to. `LocalizedStrings` is what does: one object every binding goes through, registered for you, that
announces a change of culture as a change to every string it holds.

```xml
<TextBlock Text="{Binding Strings[Home_Title]}" />
```

```csharp
public class HomeViewModel : ViewModelBase
{
    public HomeViewModel(IServiceProvider services) : base(services) =>
        Strings = services.GetRequiredService<LocalizedStrings>();

    // The whole window follows a change of culture, because every label is one of these.
    public LocalizedStrings Strings { get; }
}
```

`LanguageSelector` is the picker, handed the view model the container built — the same arrangement
as `ThemeSelector`:

```xml
<loc:LanguageSelector ViewModel="{Binding LanguageSelector}" />
```

```csharp
public LanguageSelectorViewModel LanguageSelector { get; } =
    services.GetRequiredService<LanguageSelectorViewModel>();
```

It lists the supported cultures by their **native** names — someone hunting for Spanish in an
English window is looking for "Español" — applies the choice immediately, rolls back if the service
refuses it, and follows a culture changed anywhere else.

> What this does **not** refresh is anything the culture affects without going through
> `LocalizedStrings`: a `StringFormat`, a date, a number. Those are formatted by the binding itself,
> and a binding that has not been told anything changed will not run again. Re-raise those
> properties yourself from `CultureChanged` if a screen shows them.

### Keys checked at compile time

That fallback is also why a mistyped key can ship: the application keeps working and shows
`WelcomeMessage` to the user. The package carries an analyzer that reads the project's `.resx`
files and says so first.

| | |
|---|---|
| `PGL001` | The key is in none of the `.resx` files |
| `PGL002` | The key is in the neutral `.resx` but missing from a translation |

`GetString` declares its parameter as a key, and so does `LocalizedStrings`, so every
`Strings["..."]` in the application is checked already. Put `[LocalizationKey]` on wrappers of your
own and their call sites are checked the same way:

```csharp
// Same rules, one call site: Greeting("Welcome_Back", name) is checked like any other key.
public string Greeting([LocalizationKey] string key, string name) =>
    string.Format(_localization.GetString(key), name);
```

Only constant keys are checked; one built at runtime is left alone. Keys named in XAML are not
seen either — they are not C# — but `PGL002` is about the resource files themselves, so it reports
whether or not any code reads the key.

Both are warnings. A key that resolves to nothing and a language that is missing one are defects
with no other symptom — nothing else in the build, and nothing at runtime, will ever mention them.

Change that where it does not suit the project, with a `.globalconfig` beside it (`PGL002` is
reported against `.resx` files and once per compilation, which no `.editorconfig` section matches
reliably):

```ini
is_global = true

# Stricter, for a project where an untranslated string must not ship:
dotnet_diagnostic.PGL002.severity = error

# Or quieter, where translation lags the code on purpose:
# dotnet_diagnostic.PGL002.severity = suggestion
```

```xml
<ItemGroup>
  <GlobalAnalyzerConfigFiles Include="localization.globalconfig" />
</ItemGroup>
```

---

## 🔧 Configuration

```csharp
public partial class App : PangeaApplication
{
    public override PangeaOptions ConfigurePangeaOptions(PangeaOptions options)
    {
        options.DI.AutoRegisterViewModels = true;
        options.DI.ViewModelLifetime = ServiceLifetime.Transient;

        // Assemblies to scan beyond those reachable from the entry assembly
        options.DI.AdditionalAssemblies.Add(typeof(PluginFeature).Assembly);

        options.Window.AutoDiscoverMainWindow = true;
        options.Window.MainWindowType = typeof(MainWindow);
        options.Window.MainViewModelType = typeof(MainWindowViewModel);

        return options;
    }
}
```

### Work that has to finish first

`IPangeaFeature.ConfigureApplication` runs on the UI thread and returns nothing, so the only thing a
feature can do with slow work there is start it and hope. That is right for work whose result merely
replaces a default — the shell template restores the saved culture that way — and wrong for work the
first screen cannot do without.

```csharp
public sealed class WarmCacheInitializer(ICatalog catalog) : IPangeaAsyncInitializer
{
    public string Name => "Loading the catalog";   // shown on the splash while it runs

    public int Order => 0;                          // lower runs first

    public Task InitializeAsync(CancellationToken cancellationToken) =>
        catalog.LoadAsync(cancellationToken);
}
```

```csharp
services.AddSingleton<IPangeaAsyncInitializer, WarmCacheInitializer>();
```

Every registered initializer is awaited, in order, off the UI thread, while a splash window stands
in for the main one. Register none and startup is exactly what it always was: the main window is
created and shown with nothing in between. The database feature registers one for you.

```csharp
options.Startup.ShowSplash = true;                  // false leaves the screen empty until the main window
options.Startup.SplashWindowType = typeof(Splash);  // your own; implement IPangeaSplashView for the status
options.Startup.Timeout = TimeSpan.FromMinutes(2);  // null waits forever
options.Startup.FailureBehavior = StartupFailureBehavior.Report;
```

`Report` is the default: the splash becomes the failure report and stays there, because a process
that vanishes with no window and no message is worse than one that says why. `Continue` logs it and
opens the main window anyway; `Throw` rethrows on the UI thread, where the application's unhandled
exception handling can see it.

Running past `Timeout` is one of those failures, reported as a `TimeoutException` naming the
initializer that was still going — not as the bare `A task was canceled` underneath it.

### Writing a feature

```csharp
public class TelemetryFeature : IPangeaFeature
{
    public string Name => "Telemetry";
    public Version Version => new(1, 0, 0);

    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<ITelemetry, Telemetry>();

    public void ConfigureApplication(IServiceProvider services, IPangeaApplicationContext context)
    {
        // Runs once the container is built, with the application available
    }
}
```

Discovery is by interface: any non-abstract `IPangeaFeature` in a scanned assembly is instantiated
and registered. A feature that fails to configure aborts startup naming itself, rather than leaving
the application half-wired.

### Logging

The toolkit logs through `ILogger` and registers no providers of its own. Add yours and it is picked
up:

```csharp
public override void Configure(IServiceCollection services) =>
    services.AddLogging(builder => builder.AddConsole());
```

### What startup does instead of scanning

Discovering features, view models and views used to mean walking every assembly the application can
reach and reading every type in it. All of that is knowable while the code is being compiled, so a
source generator writes it down: each project gets a `PangeaCatalog` listing what it contributes,
registered from a module initializer before anything asks.

Nothing to configure — the generator ships with the packages. What changes:

- Startup does no assembly scan. `TypeRegistry` is still there and still registered, but nothing
  makes it read anything unless something asks it a question the catalog cannot answer.
- View models are registered with a generated factory — a plain `new` with each dependency resolved
  by type — rather than left for the container to construct by reflection.
- Views and windows are created by generated constructor calls rather than `Activator`.
- Navigation requests are checked against their destinations from the catalog.

A project the generator never ran in falls back to the old path in full, and so does an application
that names extra assemblies through `options.DI.AdditionalAssemblies` — nothing was compiled
alongside those, so they are still read. The catalog is used only when the application's own
assembly has one, because the toolkit's assemblies always do and that on its own proves nothing
about the application.

This is also what makes trimming and ahead-of-time compilation reachable: a constructor called by
generated code is a constructor the trimmer can see. Reflection has not left the toolkit entirely —
validation attributes, the typed navigation arrival hook and resource discovery still use it — so
treat this as the first step rather than the finished job.

---

## 💬 Dialogs

Two questions, answered without a window being written for them.

```csharp
bool confirmed = await _dialogs.ConfirmAsync(
    "Delete order", "This cannot be undone.", "Delete it", "Keep it");

await _dialogs.AlertAsync("Saved", "Your changes have been saved.");
```

Dismissing the dialog by its window chrome is read as a cancel, so `ConfirmAsync` never returns
`true` without the user saying so. The dialog takes the application's theme, and needs a main window
to own it.

For a dialog with its own fields, or a result that is not a bool, write a view model and a window
and show it with `IWindowManager.ShowDialogAsync<TWindow, TViewModel, TResult>` — `IDialogService`
is deliberately only these two questions.

**Keyboard.** Windows and dialogs both get focus placed on their first control when they open,
unless they focused something themselves. Escape closes a dialog; it does not close a window, which
is the platform convention rather than an oversight — Alt+F4 closes windows, and Escape destroying
one holding unsaved work is a keystroke away from losing it. A secondary window can ask for it:

```xml
<Window xmlns:win="using:CdCSharp.Pangea.Windows"
        win:WindowBehavior.CloseOnEscape="True">
```

---

## 🧭 Navigation

A navigation request names where it goes, so the call site stays short and the compiler still
checks it.

```csharp
public sealed record ShowOrder(Guid Id) : INavigationRequest<OrderViewModel>;
```

```csharp
public partial class OrderViewModel : ViewModelBase, INavigationAware<ShowOrder>
{
    public OrderViewModel(IServiceProvider services) : base(services) { }

    [Binding] private Order? _order;

    public Task OnNavigatedToAsync(ShowOrder request)
    {
        Order = _orders.Find(request.Id);   // request.Id is a Guid, no cast
        return Task.CompletedTask;
    }
}
```

```csharp
// The destination is inferred from the request
await _navigation.NavigateToAsync(new ShowOrder(orderId));
await _navigation.NavigateToAsync<SettingsViewModel>();   // no data to carry
await _navigation.GoBackAsync();
```

Put a host where the content belongs and it follows along:

```xml
<Window xmlns:pangea="clr-namespace:CdCSharp.Pangea.Navigation;assembly=CdCSharp.Pangea.Navigation">
  <DockPanel>
    <StackPanel DockPanel.Dock="Left"> <!-- menu --> </StackPanel>
    <pangea:NavigationHost />
  </DockPanel>
</Window>
```

**Lifecycle.** Every view model gets three hooks from `ViewModelBase`. They now fire:

| | |
|---|---|
| `CanNavigateAwayAsync` | Return `false` to cancel the navigation - how a screen keeps unsaved work |
| `OnNavigatedFromAsync` | The screen is no longer current |
| `OnNavigatedToAsync` | The screen became current. A request arrives through `INavigationAware<TRequest>` instead |

Going back returns the same view model instance and does **not** replay the request.

**Arriving at a screen moves keyboard focus into it** — the first control that can take it, or the
host itself when the screen has none, so Tab always has somewhere to start. Set
`MovesFocusOnNavigation="False"` on a host that is not the main subject of the screen, such as a
detail pane beside a list where taking focus off the list on every selection would be maddening.

**Views are found by name**, through the same type scan the rest of the toolkit uses:
`OrderViewModel` is displayed by `OrderView`, and `MainWindowViewModel` by `MainWindow`. Register
explicitly with `IViewLocator.Register<TViewModel, TView>()` when a view does not follow either.

> A request whose destination does not implement `INavigationAware<TRequest>` would navigate and
> silently drop its data. Startup checks every request and aborts naming both sides.

---

## 🤖 AI coding agents

Pangea ships a skill that teaches an AI coding agent to use the toolkit: the mental model, the
conventions, the pitfalls, and the full list of theme resource keys.

**Every C# sample in it is compiled against the real assemblies as part of the test suite**, through
the source generator, so the guidance cannot drift away from the code. That check has already caught
a generator bug and an API that made the documented approach impossible.

Two ways to get it:

```bash
# 1. The template writes it into your project, where agents look for project skills
dotnet new pangea-app -n MyApp                  # --IncludeSkill is on by default
# -> MyApp/.claude/skills/pangea/SKILL.md       commit it and the whole team gets it
```

```bash
# 2. Download the version-pinned skill from the matching release
curl -L -o pangea-skill.zip \
  https://github.com/smaicas/CdCSharp.Pangea/releases/download/v1.0.0/pangea-skill-1.0.0.zip
unzip pangea-skill.zip -d ~/.claude/skills/     # unpacks as pangea/SKILL.md, available everywhere
```


The skill follows the usual layout: a directory named after the skill, holding a `SKILL.md` whose
frontmatter says what it is and when to reach for it, with the bulky key reference in `references/`.
The directory name is the skill's identity, so keep it `pangea`.

> Pin the skill to the Pangea version you use. Guidance from an older release describes an API that
> has since moved, and an agent will follow it confidently.

---

## 🧪 Testing your own application

```bash
dotnet add package CdCSharp.Pangea.Testing
```

A view model takes an `IServiceProvider` and asks it for what it needs, so testing one otherwise
means starting Avalonia, building the real container and waiting for a window. `PangeaTestServices`
is the same shape with test doubles in it:

```csharp
PangeaTestServices services = new();
services.Dialogs.Answering(true);

OrderViewModel screen = new(services);
screen.DeleteCommand.Execute(null);

Assert.True(screen.Deleted);
Assert.Equal("Delete ORD-0001?", services.Dialogs.Confirmations.Single().Message);
```

Commands run inline, so a command has finished when `Execute` returns. Register the application's
own services with `services.Add<IOrders>(new FakeOrders())`.

| | |
|---|---|
| `InlineUIDispatcher` | Runs everything where it was called. The default |
| `PumpingUIDispatcher` | Owned by the calling thread, runs queued work on `Drain()` — for when the question is whether a call waited |
| `RecordingDialogService` | Answers from a script and remembers how the question was worded |
| `RecordingNavigationService` | Records where a navigation was headed and what it carried |
| `InMemoryStorageService` | The same paths and the same JSON round trip, with nothing on disk |
| `DictionaryLocalizationService` | Strings from a dictionary, without satellite assemblies |
| `RecordingThemeService` | Tracks theme and variant without an application's styles |

---

## 🧪 Tests

```bash
dotnet test --project test/CdCSharp.Pangea.Core.Tests/CdCSharp.Pangea.Core.Tests.csproj
```

The suites cover the source generator, the theme (structure, resource resolution per variant,
control template smoke tests, drift against upstream Avalonia), commands and threading, startup
registries, storage, localization, navigation, the agent skill's samples, and the templates.
`test/CdCSharp.Pangea.Tests.Int` is a sample application with a control gallery for looking at the
theme by eye.

The theming, window and template tests run on `Avalonia.Headless`, so they need no display.
`test/CdCSharp.Pangea.Templates.Compile` compiles the shipped templates against the toolkit in the
working tree, so a template that stops building fails the build rather than the next person who
generates one; `CdCSharp.Pangea.Templates.Tests` then starts the shell template's application and
checks that startup, the view locator and a typed navigation request all still work. CI goes one
step further: every option combination of both templates is generated from the packed package
against the packages just built, and compiled for real.

---

## 🤝 Contributing

1. Fork the repository
2. Create a branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes
4. Push and open a Pull Request

Found a bug? Check [existing issues](https://github.com/smaicas/CdCSharp.Pangea/issues) or open a
[new one](https://github.com/smaicas/CdCSharp.Pangea/issues/new).

---

## 📄 License

MIT — see [LICENSE](LICENSE).

---

## 🙏 Acknowledgments

- **[Avalonia UI](https://avaloniaui.net/)** — the cross-platform UI framework, and the Simple theme
  the control dictionaries started from
- **[.NET](https://dotnet.microsoft.com/)** — source generators and modern C#

<div align="center">

**Made with ❤️ for the Avalonia and .NET community**

[🔝 Back to top](#-cdcsharppangea)

</div>
