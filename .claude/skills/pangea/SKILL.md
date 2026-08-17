---
name: pangea
description: Build Avalonia desktop applications with the CdCSharp.Pangea toolkit - MVVM with generated bindings, themes declared as C# palette classes, per-platform storage and localization. Use when creating or modifying an Avalonia application that references CdCSharp.Pangea, or when asked to build a desktop app with this toolkit.
---

# Pangea

Write Avalonia applications with the **CdCSharp.Pangea** toolkit.

Every C# block below is compiled against the real assemblies by `CdCSharp.Pangea.Docs.Tests`, so
what is written here builds. If an example disagrees with something you remember, the example wins.

The full list of theme resource keys is in `references/resource-keys.md`; read it when writing XAML
that binds to theme colours.

This skill describes the version of Pangea it shipped with. Match it to the package version in use.

---

## Mental model

Pangea assembles what an Avalonia application usually wires by hand:

| Concern | What Pangea gives you |
|---|---|
| MVVM | `ViewModelBase`, `RelayCommand`, and a source generator that turns fields into observable properties |
| Appearance | A theme built from C# palette classes, with light and dark as Avalonia theme variants |
| Composition | A DI container, plus **features**: self-registering units of functionality |
| Platform | Per-OS storage paths, cultures and resource strings, window management |

The unit of extension is the **feature** — a class implementing `IPangeaFeature`, found at startup
and given a chance to register services and configure the running application.

---

## Application setup

This wiring is not optional. `UsePangea()` builds the container; `PangeaApplication` runs the
features and shows the main window.

```csharp
using Avalonia;
using CdCSharp.Pangea;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp;

public partial class App : PangeaApplication
{
    public override void Configure(IServiceCollection services)
    {
        // Your own services. View models deriving from ViewModelBase are registered
        // automatically, so do not register them here.
        services.AddSingleton<IDataService, DataService>();
    }
}

public static class Program
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UsePangea();
}
```

**Do not** call `AvaloniaXamlLoader.Load(this)` yourself in `App.Initialize()` if you inherit from
`PangeaApplication` — the base class handles the lifecycle. Keep `App.axaml` as usual.

---

## View models

A view model derives from `ViewModelBase`, is `partial`, and takes `IServiceProvider`.

```csharp
using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;
using System.Collections.ObjectModel;

namespace MyApp.ViewModels;

public partial class OrderViewModel : ViewModelBase
{
    public OrderViewModel(IServiceProvider services) : base(services) { }

    [Binding] private string _customer = "";
    [Binding] private int _quantity;
    [Binding] private decimal _unitPrice;

    [Binding(ReadOnly = true)] private string _orderId = "";
    [Binding(PropertyName = "Lines")] private ObservableCollection<string> _orderLines = [];

    public decimal Total => Quantity * UnitPrice;
    public bool CanSubmit => Total > 0 && !string.IsNullOrWhiteSpace(Customer);

    public RelayCommand SubmitCommand => CreateCommand(Submit, () => CanSubmit);

    private void Submit() { }

    // Optional. Called by the generated setter, after the change, before notifications.
    partial void OnQuantityChanged() { }
}
```

### What the generator works out

- A **computed property** that reads a binding property is notified when it changes, including
  through chains: `Quantity → Total → CanSubmit`.
- A **command** whose `CanExecute` reads a property — directly, through a `bool CanX()` method, or
  through a computed property — gets `RaiseCanExecuteChanged()` in that property's setter.
- If `OnXChanged` calls a method that **mutates a collection**, whatever computed properties read
  that collection are notified.

### Rules that matter

- The class must be `partial` and derive from `ViewModelBase`; the generated setter calls
  `SetProperty`, which lives there.
- The field name determines the property: `_customer` becomes `Customer`. Use
  `[Binding(PropertyName = "...")]` to choose another.
- `[Binding(ReadOnly = true)]` generates a getter only: no setter, no change hook, no notifications.
- Write computed properties in terms of the **generated properties**, not the backing fields.
  `Quantity * UnitPrice` is tracked; `_quantity * _unitPrice` is not.

---

## Commands

`CreateCommand` is on `ViewModelBase`. Two contracts matter and both are easy to get wrong.

### Threading

A **synchronous body runs on the UI thread**, marshalled if the command is invoked from elsewhere.
That is deliberate: a command body almost always touches the view model the UI is bound to.

For background work use the `Func<Task>` overload and leave the UI thread explicitly:

```csharp
using CdCSharp.Pangea.Core.Base;

namespace MyApp.ViewModels;

public partial class ReportViewModel : ViewModelBase
{
    public ReportViewModel(IServiceProvider services) : base(services) { }

    // Runs on the UI thread. Fine for touching state, wrong for long work.
    public RelayCommand RefreshCommand => CreateCommand(() => Rows = 0);

    // Runs on the UI thread until the first await; the Task.Run leaves it explicitly.
    public RelayCommand LoadCommand => CreateCommand(async () =>
    {
        int rows = await Task.Run(() => ExpensiveCount());
        Rows = rows;
    });

    public int Rows { get; set; }

    private static int ExpensiveCount() => 42;
}
```

### Errors

Failures always reach `ViewModelBase.OnCommandError`, which you override to handle them.
`ExecuteAsync` also rethrows so an awaiting caller can react; `ICommand.Execute` cannot and does not.
A `CanExecute` that throws is reported and treated as `false`, so a broken predicate does not take
down the binding evaluating it.

```csharp
using CdCSharp.Pangea.Core.Base;
using Microsoft.Extensions.Logging;

namespace MyApp.ViewModels;

public partial class SafeViewModel : ViewModelBase
{
    private readonly ILogger<SafeViewModel> _logger;

    public SafeViewModel(IServiceProvider services, ILogger<SafeViewModel> logger) : base(services) =>
        _logger = logger;

    protected override void OnCommandError(Exception exception) =>
        _logger.LogError(exception, "Command failed");
}
```

---

## Theming

Two **independent** axes. Do not conflate them:

- **Theme** — a pair of palettes, one light and one dark. `SetTheme("Corporate")`.
- **Variant** — which of the two is showing. `SetVariant(ThemeVariant.Dark)`.

Switching theme keeps the variant, and switching variant keeps the theme.

### Declaring a theme

Inherit a palette and override only the colours you care about. **Never edit the XAML under
`Resources/`** — that is Avalonia's Simple theme vendored into the toolkit, and a test guards it
against drift.

```csharp
using Avalonia.Media;
using CdCSharp.Pangea.Theming.Palettes;

// PangeaPalette carries the light values, so a light palette overrides from the base.
public sealed class CorporateLight : PangeaPalette
{
    public override Color ThemeAccentColor => Color.Parse("#FF1B6EC2");
    public override Color ThemeBackgroundColor => Color.Parse("#FFFAFAFA");
}

public sealed class CorporateDark : DarkPalette
{
    public override Color ThemeAccentColor => Color.Parse("#FF4F9DDE");
}
```

Each colour property name **is** its resource key, and every colour also produces a brush with
`Color` swapped for `Brush`. Overriding `ThemeAccentColor` updates `ThemeAccentColor`,
`ThemeAccentBrush`, and everything derived from them — including brushes derived at reduced opacity.

### Registering it

```csharp
using Avalonia.Styling;
using CdCSharp.Pangea.Theming;
using CdCSharp.Pangea.Theming.Palettes;
using Microsoft.Extensions.DependencyInjection;

public static class ThemeRegistration
{
    public static void AddThemes(IServiceCollection services) =>
        services.Configure<ThemingOptions>(options =>
        {
            // Restyle the whole application by replacing the default entry...
            options.Themes[PangeaTheme.DefaultName] =
                new PangeaTheme(new CorporateLight(), new CorporateDark());

            // ...or add more and let the user pick.
            options.Themes["HighContrast"] = new PangeaTheme(new CorporateLight(), new CorporateDark());

            options.EnableSystemThemeDetection = true;   // follow the OS preference
            options.FallbackVariant = ThemeVariant.Dark; // when it has none
        });
}
```

### Switching at runtime

```csharp
using Avalonia.Styling;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Theming.Abstractions;
using Microsoft.Extensions.DependencyInjection;

public partial class AppearanceViewModel : ViewModelBase
{
    private readonly IThemeService _themes;

    public AppearanceViewModel(IServiceProvider services) : base(services) =>
        _themes = services.GetRequiredService<IThemeService>();

    public IReadOnlyCollection<string> Themes => _themes.AvailableThemes;

    public RelayCommand UseDarkCommand => CreateCommand(() => _themes.SetVariant(ThemeVariant.Dark));
    public RelayCommand<string> UseThemeCommand => CreateCommand<string>(name => _themes.SetTheme(name!));
}
```

In XAML, bind to the resource keys with `DynamicResource` so the UI follows theme and variant
changes. Because the palettes are Avalonia theme variants, a `ThemeVariantScope` can render part of
the UI in the opposite variant.

---

## Storage

Paths come from the platform; the service does the file work.

```csharp
using CdCSharp.Pangea.Storage.Abstractions;

public sealed record AppSettings(string Theme, int FontSize);

public class SettingsStore
{
    private readonly IStorageService _storage;
    private readonly string _path;

    public SettingsStore(IStorageService storage)
    {
        _storage = storage;
        _path = storage.GetDataFilePath("settings.json");
    }

    // Returns null when the file has never been written.
    public Task<AppSettings?> LoadAsync() => _storage.ReadJsonAsync<AppSettings>(_path);

    public Task SaveAsync(AppSettings settings) => _storage.WriteJsonAsync(_path, settings);
}
```

**Asymmetry to remember**: writes create the folders they need, reads do not. `ReadTextAsync` throws
on a missing file the way `File.ReadAllTextAsync` does; `ReadJsonAsync` returns `null`, for state
that may legitimately not exist yet.

Configure with `StorageOptions`: `ApplicationName`, `UsePortableMode`, `CustomDataPath`.

---

## Localization

```csharp
using CdCSharp.Pangea.Localization;
using Microsoft.Extensions.DependencyInjection;

public static class LocalizationRegistration
{
    public static void AddLocalization(IServiceCollection services) =>
        services.Configure<LocalizationOptions>(options =>
        {
            options.SupportedCultures = ["en-US", "es-ES"];
            options.DefaultCulture = "en-US";
            options.AutoDetectCulture = true;

            // Assemblies holding the .resx-generated resource classes.
            options.ResourceAssemblies.Add(typeof(LocalizationRegistration).Assembly);
        });
}
```

`GetString` returns the key itself when nothing resolves, so a missing translation is visible rather
than blank. `SetCulture` applies to the whole application, including threads started afterwards, and
raises `CultureChanged`. It throws `NotSupportedException` for a culture outside `SupportedCultures`.

---

## Navigation

A navigation request declares where it goes. The call site infers the destination, and the
compiler still checks the data.

```csharp
using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Navigation.Abstractions;

namespace MyApp.Navigation;

public sealed record ShowInvoice(Guid Id) : INavigationRequest<InvoiceViewModel>;

public partial class InvoiceViewModel : ViewModelBase, INavigationAware<ShowInvoice>
{
    public InvoiceViewModel(IServiceProvider services) : base(services) { }

    [Binding] private Guid _invoiceId;

    public Task OnNavigatedToAsync(ShowInvoice request)
    {
        InvoiceId = request.Id;   // typed, no cast
        return Task.CompletedTask;
    }
}
```

Navigate with the request; the destination comes from its type:

```csharp
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Navigation.Abstractions;

namespace MyApp.Navigation;

public partial class InvoiceListViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    public InvoiceListViewModel(IServiceProvider services, INavigationService navigation)
        : base(services) => _navigation = navigation;

    public RelayCommand<Guid> OpenCommand => CreateCommand<Guid>(OpenAsync);

    private Task OpenAsync(Guid id) => _navigation.NavigateToAsync(new ShowInvoice(id));
}
```

Put a host where the content belongs:

```xml
<Window xmlns:pangea="clr-namespace:CdCSharp.Pangea.Navigation;assembly=CdCSharp.Pangea.Navigation">
  <DockPanel>
    <StackPanel DockPanel.Dock="Left"> <!-- menu --> </StackPanel>
    <pangea:NavigationHost />
  </DockPanel>
</Window>
```

**Rules that matter when writing navigation code**

- A screen that takes no data implements nothing extra: `NavigateToAsync<SettingsViewModel>()`
  calls the `OnNavigatedToAsync()` override from `ViewModelBase`.
- `CanNavigateAwayAsync` returning `false` cancels the navigation. That is how a screen keeps
  unsaved work; `NavigateToAsync` returns `false` when it happens.
- `GoBackAsync()` returns the same view model instance and does **not** replay the request.
- Views are found by name: `OrderViewModel` is displayed by `OrderView`, `MainWindowViewModel` by
  `MainWindow`. Otherwise call `IViewLocator.Register<TViewModel, TView>()`.
- A request whose destination does not implement `INavigationAware<TRequest>` aborts startup. Do
  not declare a request without implementing the matching interface on the view model it names.

---

## Adding a feature

```csharp
using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

public class TelemetryFeature : IPangeaFeature
{
    public string Name => "Telemetry";
    public Version Version => new(1, 0, 0);

    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<ITelemetry, Telemetry>();

    public void ConfigureApplication(IServiceProvider services, IPangeaApplicationContext context)
    {
        // Runs after the container is built, with the application available.
    }
}
```

Discovery is by interface: any non-abstract `IPangeaFeature` in a scanned assembly is instantiated
and registered. It needs a public parameterless constructor. A feature that throws while configuring
**aborts startup** naming itself — that is intentional, a half-configured feature is worse than none.

Assemblies reachable from the entry assembly are scanned automatically. For anything else:

```csharp
using CdCSharp.Pangea;
using CdCSharp.Pangea.Core.Configuration;

public partial class PluginApp : PangeaApplication
{
    public override PangeaOptions ConfigurePangeaOptions(PangeaOptions options)
    {
        options.DI.AdditionalAssemblies.Add(typeof(TelemetryFeature).Assembly);
        return options;   // the return value is what gets used
    }
}
```

---

## Logging

The toolkit logs through `ILogger` and registers no providers. Add yours in `Configure`:

```csharp
using CdCSharp.Pangea;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public partial class LoggingApp : PangeaApplication
{
    public override void Configure(IServiceCollection services) =>
        services.AddLogging(builder => builder.AddConsole());
}
```

---

## Pitfalls

- **Computed properties must read generated properties, not backing fields.** Reading `_quantity`
  produces no dependency and the property silently never updates.
- **`CreateCommand<T>` is ambiguous for a lambda that only throws** — it fits both the `Action<T?>`
  and the `Func<T?, Task>` overload. Assign to an explicit delegate type when that matters.
- **A synchronous command body runs on the UI thread.** Long work there freezes the UI; use the
  async overload.
- **`ConfigurePangeaOptions` must return the options.** The return value is what the container uses.
- **Do not register view models yourself** — `AutoRegisterViewModels` already did.
- **Do not edit the theme XAML under `Resources/Controls/Shared`.** Declare a palette instead.
- **`ReadTextAsync` throws, `ReadJsonAsync` returns null.** Pick the one matching the situation.

---

## Checklist before finishing generated code

1. Every view model is `partial`, derives from `ViewModelBase`, and takes `IServiceProvider`.
2. Bound state uses `[Binding]` fields, and computed properties read the generated properties.
3. Long-running command bodies use the async overload.
4. Appearance comes from palette classes, not edited XAML.
5. Resource keys used in XAML exist — every palette colour, its `...Brush`, and the metrics.
6. `dotnet build` is clean, and `dotnet test` passes if the project has tests.
