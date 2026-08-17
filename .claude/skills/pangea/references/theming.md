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
