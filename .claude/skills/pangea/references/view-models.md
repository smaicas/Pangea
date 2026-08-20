# View models: busy state, failures and subscriptions

Three things every screen needs and none of them worth writing by hand.

---

## A command that fails

`OnCommandError` logs. Nothing has to be written for a command body that throws to be visible - and
before it did, a body that threw left a button that appeared to do nothing at all, with no message
and no log line.

Override it to add to that, and call the base to keep the log:

```csharp
using CdCSharp.Pangea.Core.Base;

namespace ViewModelSamples;

public partial class ReportingViewModel : ViewModelBase
{
    public ReportingViewModel(IServiceProvider services) : base(services) { }

    protected override void OnCommandError(Exception ex)
    {
        base.OnCommandError(ex);

        // Something the user can see, on top of the log line.
        LastError = ex.Message;
    }

    public string? LastError { get; private set; }
}
```

`Logger` is available to a view model for anything else worth recording. It is null when the
application configured no logging, which a test container usually has not.

---

## Busy state

**Do not write a busy flag.** Two mechanisms already cover it, and a `[Binding] private bool
_isBusy` collides with the second: the generator reports `PGB006` rather than silently hiding it.

| | |
|---|---|
| `RelayCommand.IsExecuting` | True while *that* command's body runs. `CanExecute` is false meanwhile, so the button that started the work disables itself and a second press does nothing |
| `ViewModelBase.IsBusy` | True while *any* command this view model created is running |

`IsBusy` is what the rest of the screen needs: the spinner, the other buttons, the "saving..." line.
It is raised on the UI thread, and every command is asked for `CanExecute` again as it changes - so
a command gated on it hears about work started by another one.

```csharp
using CdCSharp.Pangea.Core.Base;

namespace ViewModelSamples;

public partial class OrdersViewModel : ViewModelBase
{
    public OrdersViewModel(IServiceProvider services) : base(services) { }

    // Reads IsBusy, so it is re-evaluated whenever any command starts or finishes.
    public bool CanDelete => !IsBusy && Selected is not null;

    public string? Selected { get; set; }

    public RelayCommand SaveCommand => CreateCommand(SaveAsync);

    public RelayCommand DeleteCommand => CreateCommand(DeleteAsync, () => CanDelete);

    // No try/finally around a flag: IsBusy is true for exactly as long as this runs, however it ends.
    private Task SaveAsync() => Task.CompletedTask;

    private Task DeleteAsync() => Task.CompletedTask;
}
```

```xml
<ProgressBar IsIndeterminate="True" IsVisible="{Binding IsBusy}" />
```

---

## Subscriptions

A screen that subscribes to a service outliving it is kept alive by that service's event list. It is
invisible until the application has been used for a while, and by then it is holding every screen
the user has opened.

```csharp
using CdCSharp.Pangea.Core.Base;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace ViewModelSamples;

public interface IOrderRepository
{
    event EventHandler? Changed;
}

public partial class OrderListViewModel : ViewModelBase
{
    private readonly IOrderRepository _repository;

    public OrderListViewModel(IServiceProvider services) : base(services)
    {
        _repository = services.GetRequiredService<IOrderRepository>();

        Subscribe(handler => _repository.Changed += handler,
                  handler => _repository.Changed -= handler,
                  OnOrdersChanged);
    }

    private void OnOrdersChanged(object? sender, EventArgs e)
    {
        // ...
    }
}
```

Overloads exist for `EventHandler<TArgs>`, `INotifyPropertyChanged` and `INotifyCollectionChanged`,
and `Track(IDisposable)` takes anything else that has to be released.

**Pass a method, not a lambda.** `Subscribe(..., (_, _) => Reload())` subscribes one delegate and
unsubscribes a different one, so it never comes off. A lambda that has to be used goes into a field
first.

### Who calls `Discard`

The navigation service, for a screen it drops: **going back**, and **clearing the history**.
Navigating forward does not - that screen is on the stack and is coming back with its subscriptions
intact. It only happens when view models are transient, which is the default; a container handing
out the same instance again must not have it taken apart.

A view model held anywhere else - a shell view model, one kept in a field - is discarded by whoever
holds it, or never, if it lives as long as the application.

`Discard()` deliberately is not `Dispose()`. Microsoft's container tracks every transient
`IDisposable` it creates and holds it until the process ends, so a disposable view model would
replace this leak with a larger and quieter one: every screen ever opened, kept alive by the
container that built it.

Overriding `OnDiscarded` is where anything else the screen holds goes - a timer, a cancellation
source.
