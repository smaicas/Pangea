using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Theming.Abstractions;
using CdCSharp.Pangea.Theming.Palettes;
using CdCSharp.Pangea.Theming.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Theming;

public class ThemingFeature : IPangeaFeature
{
    public string Name => "Theming";
    public Version Version => new(1, 0, 0);

    public void ConfigureServices(IServiceCollection services)
    {
        // Defaults only; the application overrides them with its own services.Configure call.
        services.Configure<ThemingOptions>(_ => { });

        services.AddSingleton<IThemeService, ThemeService>();
    }

    /// <summary>
    /// Adds the toolkit styles, registers the application's themes, and selects the starting
    /// theme and variant.
    /// </summary>
    /// <remarks>
    /// Failures are left to propagate: <see cref="Services.FeatureRegistry"/> names the feature and
    /// aborts startup, which beats an application running with half a theme.
    /// </remarks>
    public void ConfigureApplication(IServiceProvider serviceProvider, IPangeaApplicationContext applicationContext)
    {
        ThemingOptions options = serviceProvider.GetRequiredService<IOptions<ThemingOptions>>().Value;
        IThemeService themeService = serviceProvider.GetRequiredService<IThemeService>();

        Dispatcher.UIThread.Invoke(() =>
        {
            if (!applicationContext.HasStyle<PangeaUI>())
            {
                applicationContext.AddStyle(new PangeaUI());
            }

            foreach (KeyValuePair<string, PangeaTheme> theme in options.Themes)
            {
                themeService.RegisterTheme(theme.Key, theme.Value);
            }

            themeService.SetTheme(options.DefaultTheme ?? PangeaTheme.DefaultName);
            themeService.SetVariant(ResolveInitialVariant(options));
        });
    }

    private static ThemeVariant ResolveInitialVariant(ThemingOptions options)
    {
        if (options.EnableSystemThemeDetection && DetectSystemVariant() is { } detected) return detected;

        return options.FallbackVariant;
    }

    /// <summary>
    /// Asks the platform what the user's colour preference is. Note this is the platform setting,
    /// not <c>Application.RequestedThemeVariant</c>, which nothing has set yet at startup.
    /// </summary>
    private static ThemeVariant? DetectSystemVariant() =>
        Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant switch
        {
            PlatformThemeVariant.Dark => ThemeVariant.Dark,
            PlatformThemeVariant.Light => ThemeVariant.Light,
            _ => null
        };
}
