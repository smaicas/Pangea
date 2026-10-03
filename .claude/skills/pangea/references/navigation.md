# Navigation: transitions and composed screens

Two things beyond moving between screens. Read this before animating a host or building a screen
out of panels.

## Composing a screen out of panels

The naming rule the host uses - `OrderViewModel` is displayed by `OrderView`, `MainWindowViewModel`
by `MainWindow` - applies to **any content**, not only the host. The navigation feature installs an
application-wide data template backed by `IViewLocator`, so a screen made of panels binds their
view models and nothing else:

```csharp
using CdCSharp.Pangea.Core.Base;

namespace MyApp.Composition;

public class CustomerDetailsViewModel : ViewModelBase
{
    public CustomerDetailsViewModel(IServiceProvider services) : base(services) { }
}

public class CustomerScreenViewModel : ViewModelBase
{
    public CustomerScreenViewModel(IServiceProvider services, CustomerDetailsViewModel details)
        : base(services) => Details = details;

    public CustomerDetailsViewModel Details { get; }
}
```

```xml
<!-- Shows CustomerDetailsView, with Details as its data context -->
<ContentControl Content="{Binding Details}" />
```

- Do not put `<views:CustomerDetailsView />` in the XAML and wire its data context by hand, and do
  not write a `DataTemplate` for a type the convention already covers.
- A panel that **shows the type name** - `MyApp.Composition.CustomerDetailsViewModel` - has no view
  the convention can find. Rename the view, or call `IViewLocator.Register<TViewModel, TView>()`.
- A `DataTemplate` the application declares for the type still wins, as does any template closer
  to the control. Items of an `ItemsControl` are templated the same way when it sets no
  `ItemTemplate`.
- `IViewLocator.CanLocate(type)` answers whether a view would be found, without throwing.

## Screen transitions

The host is a `TransitioningContentControl`, so screens can animate:

```xml
<nav:NavigationHost MovesFocusOnNavigation="False">
  <nav:NavigationHost.PageTransition>
    <CrossFade Duration="0:0:0.18" />
  </nav:NavigationHost.PageTransition>
</nav:NavigationHost>
```

It defaults to none. Prefer a cross fade to a slide: the host cannot know which way the navigation
went, and a slide that runs backwards on Back feels worse than no slide at all.
