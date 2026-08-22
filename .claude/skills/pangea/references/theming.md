# Theming in depth

How a Pangea theme is built, and every rule that governs it.

## Palettes and themes

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

## Sizing

Colours vary between light and dark; sizes do not, so they live once at the root of the theme in
`ThemeMetrics` rather than in the palettes. Padding, minimum heights, corner radii, control glyph
sizes and the three font sizes are all metrics, and every control theme reads them with
`DynamicResource` — nothing in the vendored dictionaries hardcodes a size any more. The full list of
keys and their values is in `resource-keys.md`.

### Density

Two sets ship. `ThemeMetrics.Values` is sized for a pointer and is what a theme uses unless told
otherwise; `ThemeMetrics.Touch` is the same set sized for a thumb — nothing tappable below 48, and
the type a step up to match. A phone application picks one when it builds its theme:

```csharp
using CdCSharp.Pangea.Theming;
using CdCSharp.Pangea.Theming.Palettes;
using Microsoft.Extensions.DependencyInjection;

public static class TouchThemeRegistration
{
    public static void AddTouchTheme(IServiceCollection services) =>
        services.Configure<ThemingOptions>(options =>
            options.Themes[PangeaTheme.DefaultName] =
                new PangeaTheme(new CorporateLight(), new CorporateDark(), ThemeMetrics.Touch));
}
```

That one argument reaches every control, including the ones nobody remembers to style — which is
where a 32-high combo box on a phone comes from. `ThemeMetrics.Resize` builds a set of your own on
top of the defaults, so a metric added to the toolkit later is inherited rather than left as a hole.

### Overriding one key

Because a metric is an ordinary resource, an application resizes controls by defining the same key.
Application resources are consulted before application styles, and the theme is a style, so the
application's value wins:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:sys="using:System">
  <Application.Resources>
    <sys:Double x:Key="ComboBoxMinHeight">44</sys:Double>
    <Thickness x:Key="ComboBoxPadding">12,10</Thickness>
    <sys:Double x:Key="FontSizeNormal">16</sys:Double>
  </Application.Resources>
</Application>
```

Every control that reads the metric follows, so a touch-friendly build is a handful of keys rather
than a restyle. Override the metric, not the control: a `Style` that sets `MinHeight` on one control
type solves the same problem for that type alone, and the next control still comes out cramped.
