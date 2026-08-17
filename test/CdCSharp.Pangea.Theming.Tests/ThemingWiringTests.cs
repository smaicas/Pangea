using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Theming.Abstractions;
using CdCSharp.Pangea.Theming.Palettes;
using CdCSharp.Pangea.Theming.Services;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Theming.Tests;

/// <summary>
/// Startup wiring for theming, which lives entirely in <see cref="ThemingFeature"/>: it installs
/// the styles, registers the application's themes, and picks the starting theme and variant.
/// </summary>
public class ThemingWiringTests
{
    private static int PangeaUIStyleCount => Application.Current!.Styles.Count(style => style is PangeaUI);

    private static (ThemingFeature Feature, ThemeService Service, StubContext Context) Arrange(ThemingOptions options)
    {
        ThemeService service = new();
        return (new ThemingFeature(), service, new StubContext(service, options));
    }

    [AvaloniaFact]
    public void ConfigureApplication_DoesNotAddASecondPangeaUI()
    {
        // The test application already installed one, exactly as a real application does.
        int before = PangeaUIStyleCount;
        (ThemingFeature feature, _, StubContext context) = Arrange(new ThemingOptions());

        feature.ConfigureApplication(context, context);

        Assert.Equal(1, before);
        Assert.Equal(1, PangeaUIStyleCount);
    }

    [AvaloniaFact]
    public void ConfigureApplication_StartsOnTheToolkitTheme()
    {
        (ThemingFeature feature, ThemeService service, StubContext context) = Arrange(new ThemingOptions());

        feature.ConfigureApplication(context, context);

        Assert.Equal(PangeaTheme.DefaultName, service.CurrentTheme);
    }

    [AvaloniaFact]
    public void ConfigureApplication_UsesTheFallbackVariantWhenDetectionIsOff()
    {
        (ThemingFeature feature, ThemeService service, StubContext context) = Arrange(new ThemingOptions
        {
            EnableSystemThemeDetection = false,
            FallbackVariant = ThemeVariant.Light
        });

        feature.ConfigureApplication(context, context);

        Assert.Equal(ThemeVariant.Light, service.CurrentVariant);
        Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void ConfigureApplication_RegistersTheApplicationsThemes()
    {
        // This is how an application ships its own look: hand the feature a theme by name.
        ThemingOptions options = new();
        options.Themes["Corporate"] = new PangeaTheme(new LightPalette(), new DarkPalette());

        (ThemingFeature feature, ThemeService service, StubContext context) = Arrange(options);

        feature.ConfigureApplication(context, context);

        Assert.Contains("Corporate", service.AvailableThemes);
        Assert.Contains(PangeaTheme.DefaultName, service.AvailableThemes);
    }

    [AvaloniaFact]
    public void ConfigureApplication_HonoursTheConfiguredDefaultTheme()
    {
        ThemingOptions options = new() { DefaultTheme = "Corporate" };
        options.Themes["Corporate"] = new PangeaTheme(new LightPalette(), new DarkPalette());

        (ThemingFeature feature, ThemeService service, StubContext context) = Arrange(options);

        feature.ConfigureApplication(context, context);

        Assert.Equal("Corporate", service.CurrentTheme);
    }

    [AvaloniaFact]
    public void ReplacingTheToolkitTheme_IsHowAnApplicationRestylesEverything()
    {
        // Overriding the default entry keeps every control theme and swaps only the palettes.
        ThemingOptions options = new();
        options.Themes[PangeaTheme.DefaultName] = new PangeaTheme(new MagentaPalette(), new MagentaPalette());

        (ThemingFeature feature, ThemeService service, StubContext context) = Arrange(options);
        feature.ConfigureApplication(context, context);

        Assert.True(Application.Current!.TryGetResource("ThemeAccentBrush", ThemeVariant.Dark, out object? accent));
        Assert.Equal(Avalonia.Media.Colors.Magenta, ((Avalonia.Media.ISolidColorBrush)accent!).Color);
        Assert.Equal(PangeaTheme.DefaultName, service.CurrentTheme);
    }

    private sealed class MagentaPalette : PangeaPalette
    {
        public override Avalonia.Media.Color ThemeAccentColor => Avalonia.Media.Colors.Magenta;
    }

    /// <summary>Doubles as the application context and the service provider the feature asks.</summary>
    private sealed class StubContext(IThemeService themeService, ThemingOptions options)
        : IPangeaApplicationContext, IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IThemeService)) return themeService;
            if (serviceType == typeof(IOptions<ThemingOptions>)) return Options.Create(options);
            return null;
        }

        public void AddStyle(object style)
        {
            if (style is IStyle avaloniaStyle) Application.Current!.Styles.Add(avaloniaStyle);
        }

        public void RemoveStyle(object style)
        {
            if (style is IStyle avaloniaStyle) Application.Current!.Styles.Remove(avaloniaStyle);
        }

        public bool HasStyle<T>() where T : class => Application.Current!.Styles.Any(style => style is T);

        public T? GetRequiredService<T>() where T : class => GetService(typeof(T)) as T;

        public object? GetApplication() => Application.Current;
    }
}
