---
name: pangea
description: Build Avalonia desktop applications with the CdCSharp.Pangea toolkit - MVVM with generated bindings, themes declared as C# palette classes, per-platform storage and localization. Use when creating or modifying an Avalonia application that references CdCSharp.Pangea, or when asked to build a desktop app with this toolkit.
---

# Pangea

Write Avalonia applications with the **CdCSharp.Pangea** toolkit.

Every C# block below is compiled against the real assemblies by `CdCSharp.Pangea.Docs.Tests`, so
what is written here builds. If an example disagrees with something you remember, the example wins.

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
using Avalonia.Markup.Xaml;
using CdCSharp.Pangea;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp;

public partial class App : PangeaApplication
{
    // Loads App.axaml, as in any Avalonia application. PangeaApplication overrides
    // OnFrameworkInitializationCompleted, not Initialize.
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

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

**Do not** override `OnFrameworkInitializationCompleted` — that is the one `PangeaApplication`
uses to run the features and show the main window. Keep `App.axaml` as usual.

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
- If a change hook **fills a collection** - in its own body, or through any method it calls,
  however deep - the computed properties that read that collection are notified. Only that
  collection: readers of a collection the hook did not touch stay quiet. The collection type does
  not matter; `List<T>` behaves like `ObservableCollection<T>` here, because what drives the
  notification is the hook, not the collection.

### Rules that matter

- The class must be `partial` and derive from `ViewModelBase`; the generated setter calls
  `SetProperty`, which lives there.
- The field name determines the property: `_customer` becomes `Customer`. Use
  `[Binding(PropertyName = "...")]` to choose another.
- `[Binding(ReadOnly = true)]` generates a getter only: no setter, no change hook, no notifications.
- Write computed properties in terms of the **generated properties**, not the backing fields.
  `Quantity * UnitPrice` is tracked; `_quantity * _unitPrice` is not.

---

## Validation

Rules go on the field, beside the value they constrain. The generator copies them onto the
generated property and validates on every set, through `INotifyDataErrorInfo` - which Avalonia
already listens to, so a `TextBox` shows the error without being told.

```csharp
using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;
using System.ComponentModel.DataAnnotations;

namespace MyApp.ViewModels;

public partial class SignUpViewModel : ViewModelBase
{
    public SignUpViewModel(IServiceProvider services) : base(services) { }

    [Binding]
    [Required(ErrorMessage = "An email is required.")]
    [EmailAddress(ErrorMessage = "That is not an email.")]
    private string _email = "";

    [Binding]
    [Range(18, 120)] private int _age;

    // HasErrors comes from ViewModelBase and changes as the user types
    public RelayCommand SignUpCommand => CreateCommand(SignUp, () => !HasErrors);

    private void SignUp() { }
}
```

**Rules that matter when writing validation**

- Any `ValidationAttribute` works, including one the application writes itself. The rules are
  evaluated by `System.ComponentModel.DataAnnotations`, not re-implemented by the generator.
- A property is validated when it is **set**, so a form that has not been touched shows no errors.
  Call `ValidateAll()` - on `ViewModelBase`, returns whether the view model is now valid - before
  saving.
- `HasErrors` raises `PropertyChanged`, so a command guarded by it is re-evaluated on its own.
- `GetErrors(propertyName)` returns that property's messages; `GetErrors(null)` returns all of them.

---

## Commands

`CreateCommand` is on `ViewModelBase`. Two contracts matter and both are easy to get wrong.

**A command will not run twice at once**: while its body runs `IsExecuting` is true and `CanExecute`
is false. `ViewModelBase.IsBusy` is true while any of its commands run and re-asks every command as
it changes, which is what disables the rest of the screen - do **not** write a busy flag of your
own, a `[Binding] private bool _isBusy` is the compile error `PGB006`. Failures reach
`OnCommandError`, which logs. A screen subscribing to something that outlives it must use
`Subscribe(...)`. All three are in `references/view-models.md`.

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

A theme is a `PangeaTheme` built from two `PangeaPalette` classes, one per variant. Inherit
`PangeaPalette` or `DarkPalette`, override the colours you want, and every brush derived from them
follows. Register it through `ThemingOptions.Themes`, and override the entry named
`PangeaTheme.DefaultName` to restyle the toolkit's own look.

The palette classes, the naming rule between colours and brushes, the irregular keys and the full
worked example are in [references/theming.md](references/theming.md); every resource key is in
[references/resource-keys.md](references/resource-keys.md).

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

JSON that will not convert throws `StorageSerializationException`, not an `IOException`: "the disk
is full" is worth retrying and "this cannot be serialized" never will be. Catch them apart.

Configure with `StorageOptions`: `ApplicationName`, `UsePortableMode`, `CustomDataPath`.

---

## Database

Entity Framework Core, installed separately (`CdCSharp.Pangea.Data.Sqlite`). A view model asks for
`IPangeaDbContext<TContext>` and never for a `DbContext`; migrations run at startup, behind the
splash window, with a backup taken first. Read `references/database.md` before writing data access.

---

## Mobile

Android and iOS are single-view platforms: a `Window` cannot be constructed there at all. An
application with mobile heads gives the toolkit a `MainView` control instead of a `MainWindow`, and
the splash and every dialog are layered over it rather than opened beside it. `IDialogService`,
navigation, theming and storage are unchanged. Arriving at a screen does not take focus there - it
would open the system keyboard over what the user came to read. Credentials go in `ISecretStore`,
never in the settings file, and `IConnectivity` says whether the network is worth trying; both are
registered for you and both are replaceable by the platform head.

Read [references/mobile.md](references/mobile.md) before adding a mobile head.

---

## Supabase

A shared Postgres backend, installed separately (`CdCSharp.Pangea.Supabase`). A view model asks for
`ISupabaseClientProvider` and never for a `Supabase.Client`; writes made offline go into `IOutbox`
and are replayed. Read [references/supabase.md](references/supabase.md) before writing any code
against it.

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
than blank - and can ship unnoticed, which is why an analyzer checks keys against the `.resx` files
(`PGL001`, `PGL002`). Bind labels through `LocalizedStrings` and offer the choice with
`LanguageSelector`, both from the container; do **not** write your own indexer.

```xml
<TextBlock Text="{Binding Strings[Home_Title]}" />
<loc:LanguageSelector ViewModel="{Binding LanguageSelector}" />
```

Read `references/localization.md` before writing localized code: the picker, the indexer, what does
and does not re-read on a culture change, `[LocalizationKey]`, and the rules' severity.

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
- Arriving at a screen moves keyboard focus into it - the first control that can take it, or
  the host itself when the screen has none. Set `MovesFocusOnNavigation="False"` on a host
  that is not the main subject of the screen, such as a detail pane beside a list - **and on
  every host in a phone application**, where focusing a text box summons the system keyboard
  over half the screen on arrival.
- The host is a `TransitioningContentControl`, so screens can animate:

  ```xml
  <nav:NavigationHost MovesFocusOnNavigation="False">
    <nav:NavigationHost.PageTransition>
      <CrossFade Duration="0:0:0.18" />
    </nav:NavigationHost.PageTransition>
  </nav:NavigationHost>
  ```

  It defaults to none. Prefer a cross fade to a slide: the host cannot know which way the
  navigation went, and a slide that runs backwards on Back feels worse than no slide at all.
- A request whose destination does not implement `INavigationAware<TRequest>` aborts startup. Do
  not declare a request without implementing the matching interface on the view model it names.

---

## Dialogs

Two questions the toolkit answers without a window being written for them.

```csharp
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Dialogs;

namespace MyApp.ViewModels;

public partial class OrdersViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;

    public OrdersViewModel(IServiceProvider services, IDialogService dialogs) : base(services) =>
        _dialogs = dialogs;

    public RelayCommand DeleteCommand => CreateCommand(DeleteAsync);

    private async Task DeleteAsync()
    {
        bool confirmed = await _dialogs.ConfirmAsync(
            "Delete order", "This cannot be undone.", "Delete it", "Keep it");

        if (!confirmed) return;

        await _dialogs.AlertAsync("Deleted", "The order is gone.");
    }
}
```

**Rules that matter when writing dialog code**

- Dismissing the dialog by its window chrome is read as a cancel, so `ConfirmAsync` returns
  `false`. It never returns `true` without the user saying so.
- A dialog needs a main window to own it, and says so if there is none.
- The dialog takes the application's theme; there is nothing to style.
- For a dialog with its own fields or a result that is not a bool, write a view model and a window
  and use `IWindowManager.ShowDialogAsync<TWindow, TViewModel, TResult>`. `IDialogService` is
  deliberately only these two questions.

Windows and dialogs also have rules about size, scrolling and the keyboard that are easy to get
wrong and expensive to notice: read `references/windows.md` before writing a window.

---

## Adding a feature

Extending the toolkit itself - writing an `IPangeaFeature` that registers its own services and
configures the running application - is described in
[references/extending-pangea.md](references/extending-pangea.md).

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
- **`ReadTextAsync` throws, `ReadJsonAsync` returns null** for a file that is not there. A file
  that is there but unreadable throws in both cases: corrupt data is not absent data.
- **`GetDataFilePath` only names files inside the data folder.** A relative name, subfolders
  included, is fine; an absolute path or one climbing out with `..` is rejected. Pass a
  path from outside straight to the read and write methods instead.

### What the generator reports

The generator refuses to emit for a class it cannot generate correctly, and says why. Reading the
code, these are the mistakes it catches:

| | |
|---|---|
| `PGB001` | The class has `[Binding]` fields and is not `partial` |
| `PGB002` | The class has `[Binding]` fields and inherits no `SetProperty`/`OnPropertyChanged` |
| `PGB003` | Two `[Binding]` fields would generate the same property |
| `PGB004` | The generated property name is already declared in the class |
| `PGB005` | `[Binding]` on a `static` field, which is ignored (warning) |
| `PGB006` | The generated property hides a member of a base class (warning) |

A class that trips an error generates nothing, so a missing property is the symptom to look for.

### What the localization analyzer reports

| | |
|---|---|
| `PGL001` | The resource key is in none of the project's `.resx` files |
| `PGL002` | The key is in the neutral `.resx` but missing from a translation |

Both are warnings, so they fail a build that treats warnings as errors. Lower one to `suggestion`
in a `.globalconfig` where translation lags the code on purpose.

---

## Testing a view model

`CdCSharp.Pangea.Testing` is the package for the test project: `PangeaTestServices` is the container
a view model needs, with test doubles in it. Commands run inline, dialogs answer from a script,
navigations are recorded rather than performed, and storage stays in memory.

Read `references/testing.md` before writing tests for a Pangea application.

---

## Generated startup

Startup reads a generated catalog rather than scanning assemblies. Nothing to write and nothing to
configure; a type in `CdCSharp.Pangea.Generated.*` is generated, so do not edit or reference it.
The rest is in `references/extending-pangea.md`.

---

## Checklist before finishing generated code

1. Every view model is `partial`, derives from `ViewModelBase`, and takes `IServiceProvider`.
2. Bound state uses `[Binding]` fields, and computed properties read the generated properties.
3. Long-running command bodies use the async overload.
4. Appearance comes from palette classes, not edited XAML.
5. Resource keys used in XAML exist — every palette colour, its `...Brush`, and the metrics.
6. `dotnet build` is clean, and `dotnet test` passes if the project has tests.
