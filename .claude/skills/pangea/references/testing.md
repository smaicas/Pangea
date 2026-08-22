# Testing a Pangea application

`CdCSharp.Pangea.Testing` is the package for the test project.

```bash
dotnet add package CdCSharp.Pangea.Testing
```

## PangeaTestServices

A view model takes an `IServiceProvider` and asks it for what it needs, so testing one otherwise
means starting Avalonia, building the real container and waiting for a window. `PangeaTestServices`
is the same shape with test doubles in it: commands run inline, dialogs answer from a script,
navigations are recorded rather than performed, and storage stays in memory.

```csharp
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Testing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace MyApp.Tests;

public class OrderViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;

    public OrderViewModel(IServiceProvider services) : base(services) =>
        _dialogs = services.GetRequiredService<IDialogService>();

    public bool Deleted { get; private set; }

    public RelayCommand DeleteCommand => CreateCommand(DeleteAsync);

    private async Task DeleteAsync()
    {
        if (await _dialogs.ConfirmAsync("Delete", "Delete the order?")) Deleted = true;
    }
}

public static class OrderViewModelTests
{
    public static bool DeletingAsksFirst()
    {
        PangeaTestServices services = new();
        services.Dialogs.Answering(true);

        OrderViewModel screen = new(services);
        screen.DeleteCommand.Execute(null);

        return screen.Deleted && services.Dialogs.Confirmations.Count == 1;
    }
}
```

Commands run inline, so a command has finished by the time `Execute` returns — there is nothing to
pump and nothing to await.

Register the application's own services alongside the doubles:

```csharp
using CdCSharp.Pangea.Testing;

namespace MyApp.Tests;

public interface IOrders;

public sealed class FakeOrders : IOrders;

public static class Registration
{
    public static PangeaTestServices Build() => new PangeaTestServices().Add<IOrders>(new FakeOrders());
}
```

## Asserting on what a screen did

Each double is inspectable where the real service is not.

```csharp
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Navigation.Abstractions;
using CdCSharp.Pangea.Testing;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace MyApp.Tests.Navigating;

public sealed class DetailViewModel(IServiceProvider services) : ViewModelBase(services);

public sealed record ShowDetail(string Reference) : INavigationRequest<DetailViewModel>;

public class ListViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    public ListViewModel(IServiceProvider services) : base(services) =>
        _navigation = services.GetRequiredService<INavigationService>();

    public RelayCommand OpenCommand => CreateCommand(() => _navigation.NavigateToAsync(new ShowDetail("ORD-0001")));
}

public static class ListViewModelTests
{
    public static bool OpeningNavigatesWithTheReference()
    {
        PangeaTestServices services = new();

        new ListViewModel(services).OpenCommand.Execute(null);

        return services.Navigation.LastDestination == typeof(DetailViewModel)
            && services.Navigation.LastRequest<ShowDetail>()?.Reference == "ORD-0001";
    }
}
```

## What is in the box

| | |
|---|---|
| `InlineUIDispatcher` | Runs everything where it was called. The default |
| `PumpingUIDispatcher` | Owned by the calling thread, runs queued work on `Drain()` — for when the question is whether a call waited |
| `RecordingDialogService` | `Answering(...)` scripts the user's answers; `Confirmations` and `Alerts` record what was asked |
| `RecordingNavigationService` | `Navigations`, `LastDestination`, `LastRequest<T>()`; `Refuse` makes every navigation report that it was cancelled |
| `InMemoryStorageService` | The same paths and the same JSON round trip, with nothing on disk |
| `DictionaryLocalizationService` | Strings from a dictionary, without satellite assemblies |
| `RecordingThemeService` | `ThemesSet` and `VariantsSet`, without an application's styles |

`Localization` and `Strings` are registered too, so a screen that takes `LocalizedStrings` in its
constructor can be built without arranging anything: the dictionary starts empty and an unknown key
answers with the key itself. Fill `services.Localization` when what a screen says is the thing being
asserted.

Pass a dispatcher to the constructor to swap the default:
`new PangeaTestServices(new PumpingUIDispatcher())`.

## The templates ship this

Every `dotnet new` template generates a `<Name>.Tests` project alongside the application, holding
one test per convention: a `[Binding]` field notifies, a computed property is notified by what it
reads, and a command re-evaluates when its dependency changes. Copy that file for your own screens.

The command test is the one worth keeping. Most of what a generated view model does is visible the
moment the application runs; a command that never re-evaluates its `CanExecute` looks like a button
that is simply disabled, which is a bug that costs an afternoon to recognise.

## Testing the application itself

For the parts that need a real application — the window manager, the navigation host, control
templates — use `Avalonia.Headless.XUnit` and start the application with `UsePangea()`:

```csharp
using Avalonia;
using Avalonia.Headless;
using CdCSharp.Pangea;

namespace MyApp.Tests.Headless;

// The application under test, as the project declares it.
public partial class App : PangeaApplication;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .UsePangea();
}
```

A headless session has no application lifetime and will not accept one, so no window is shown and
`IWindowManager.GetMainWindow()` returns null. Everything else runs: the container is built, the
features configure the application, and view models resolve.
