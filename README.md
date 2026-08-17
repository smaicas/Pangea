# 🌍 CdCSharp.Pangea

<div align="center">

[![NuGet Version](https://img.shields.io/nuget/v/CdCSharp.Pangea?style=flat-square&logo=nuget&logoColor=white&label=NuGet&color=004880)](https://www.nuget.org/packages/CdCSharp.Pangea)
[![NuGet Downloads](https://img.shields.io/nuget/dt/CdCSharp.Pangea?style=flat-square&logo=nuget&logoColor=white&label=Downloads&color=004880)](https://www.nuget.org/packages/CdCSharp.Pangea)
[![Build Status](https://img.shields.io/github/actions/workflow/status/smaicas/CdCSharp.Pangea/.github/workflows/nuget-publish.yml?style=flat-square&logo=github&label=Build)](https://github.com/smaicas/CdCSharp.Pangea/actions)
[![License](https://img.shields.io/github/license/smaicas/CdCSharp.Pangea?style=flat-square&logo=opensourceinitiative&logoColor=white&label=License&color=green)](https://github.com/smaicas/CdCSharp.Pangea/blob/main/LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia](https://img.shields.io/badge/Avalonia-12.1.1-663399?style=flat-square&logo=avalonia)](https://avaloniaui.net/)

**An Avalonia toolkit: MVVM with generated bindings, themes as C# classes, storage and localization**

[📦 Installation](#-installation) • [🚀 Quick start](#-quick-start) • [🧠 Binding](#-binding) • [🎨 Theming](#-theming) • [💾 Storage](#-storage) • [🌐 Localization](#-localization) • [🧭 Navigation](#-navigation) • [🤖 AI agents](#-ai-coding-agents)

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

### Start from the template

```bash
dotnet new install CdCSharp.Pangea.Templates
dotnet new pangea-app -n MyApp
cd MyApp && dotnet run
```

You get a running Avalonia application: the startup wiring, a window, and a sample view model and
palette showing the toolkit's conventions.

| Option | Default | |
|---|---|---|
| `--IncludeSkill` | `true` | Copy the [agent skill](#-ai-coding-agents) into the project |
| `--Sample` | `true` | Include the sample view model and palette |
| `--PangeaVersion` | matches the template | Version of the Pangea packages to reference |

### Add to an existing project

```bash
dotnet add package CdCSharp.Pangea
```

That is the package to install: besides pulling in every feature, it is where the application model
lives — `PangeaApplication`, `UsePangea()` and the window manager.

The features are also published on their own, for using a piece of the toolkit as a plain library
without the Pangea application model. Each depends only on `CdCSharp.Pangea.Core`:

```bash
dotnet add package CdCSharp.Pangea.Binding       # [Binding] attribute + source generator
dotnet add package CdCSharp.Pangea.Theming       # palettes, themes, theme service
dotnet add package CdCSharp.Pangea.Storage       # per-platform paths and file access
dotnet add package CdCSharp.Pangea.Localization  # cultures and resource strings
dotnet add package CdCSharp.Pangea.Navigation    # typed navigation requests and a host
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
| Collections | If `OnXChanged` calls a method that mutates a collection, whatever reads that collection is notified |

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

## 🧪 Tests

```bash
dotnet test --project test/CdCSharp.Pangea.Core.Tests/CdCSharp.Pangea.Core.Tests.csproj
```

Seven suites cover the source generator, the theme (structure, resource resolution per variant,
control template smoke tests, drift against upstream Avalonia), commands and threading, startup
registries, storage, localization, and the agent skill's samples.
`test/CdCSharp.Pangea.Tests.Int` is a sample application with a control gallery for looking at the
theme by eye.

The theming and window tests run on `Avalonia.Headless`, so they need no display. The template is
verified in CI instead: every option combination is generated against the packages just built and
compiled for real.

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
