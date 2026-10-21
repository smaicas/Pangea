using Avalonia.Media;
using Avalonia.Styling;
using CdCSharp.Pangea.Apps.Wire;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Localization.Abstractions;
using CdCSharp.Pangea.Theming;
using CdCSharp.Pangea.Theming.Abstractions;
using CdCSharp.Pangea.Theming.Palettes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CdCSharp.Pangea.Apps;

/// <summary>
/// Wears what the host decided while it is connected, and goes back to the application's own
/// choices when it is not.
/// </summary>
/// <remarks>
/// The application's own theme is remembered the first time the host's replaces it, so losing the
/// host - closing it, or it crashing - puts back exactly what the application had, not a default.
/// Sizes are never the host's to decide: the host's palette is combined with the application's own
/// metrics, so an application built for touch stays built for touch.
/// </remarks>
internal sealed class HostEnvironmentApplier(
    IAppHostContext host,
    IServiceProvider services,
    IUIDispatcher dispatcher,
    IOptions<AppsOptions> options,
    ILogger<HostEnvironmentApplier> logger)
{
    /// <summary>Name the host's theme is registered under, beside the application's own.</summary>
    internal const string HostThemeName = "pangea-host";

    private string? _appliedTheme;
    private string? _ownTheme;
    private ThemeVariant? _ownVariant;

    private string? _appliedCulture;
    private string? _ownCulture;

    public void Attach()
    {
        host.Changed += (_, _) => dispatcher.Post(Update);
        dispatcher.Post(Update);
    }

    /// <summary>Brings the application in line with the host. Runs on the UI thread.</summary>
    internal void Update()
    {
        try
        {
            AppEnvironment? environment = host.IsHosted ? host.Environment : null;

            if (options.Value.FollowHostTheme) UpdateTheme(environment?.Theme);
            if (options.Value.FollowHostCulture) UpdateCulture(environment?.Culture);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The host's environment could not be applied");
        }
    }

    private void UpdateTheme(AppThemeSnapshot? wanted)
    {
        if (services.GetService<IThemeService>() is not { } themes) return;

        // Compared as text: every frame brings new dictionaries, and two snapshots that say the
        // same thing should not rebuild the theme.
        string? key = wanted is null ? null : JsonSerializer.Serialize(wanted, WireJson.Options);

        if (key == _appliedTheme) return;

        if (wanted is null)
        {
            if (_ownTheme is not null) themes.SetTheme(_ownTheme);
            if (_ownVariant is not null) themes.SetVariant(_ownVariant);

            _appliedTheme = null;
            return;
        }

        if (_appliedTheme is null)
        {
            _ownTheme = themes.CurrentTheme;
            _ownVariant = themes.CurrentVariant;
        }

        IReadOnlyDictionary<string, object>? metrics =
            services.GetService<IOptions<ThemingOptions>>()?.Value.Themes.GetValueOrDefault(_ownTheme ?? PangeaTheme.DefaultName)?.Metrics;

        PangeaTheme theme = new(
            new DictionaryPalette(Parse(wanted.Light), wanted.PickerHighlightOpacity),
            new DictionaryPalette(Parse(wanted.Dark), wanted.PickerHighlightOpacity),
            metrics);

        themes.RegisterTheme(HostThemeName, theme);
        themes.SetTheme(HostThemeName);
        themes.SetVariant(wanted.Variant switch
        {
            AppThemeVariant.Light => ThemeVariant.Light,
            AppThemeVariant.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        });

        _appliedTheme = key;
    }

    private void UpdateCulture(string? wanted)
    {
        if (services.GetService<ILocalizationService>() is not { } localization) return;

        if (wanted == _appliedCulture) return;

        if (wanted is null)
        {
            if (_ownCulture is not null) localization.SetCulture(_ownCulture);

            _appliedCulture = null;
            return;
        }

        // A culture the application was not translated into would show its keys; keep its own.
        if (!localization.SupportedCultures.Any(culture => string.Equals(culture.Name, wanted, StringComparison.OrdinalIgnoreCase))) return;

        _ownCulture ??= localization.CurrentCulture.Name;
        localization.SetCulture(wanted);
        _appliedCulture = wanted;
    }

    private static Dictionary<string, Color> Parse(IReadOnlyDictionary<string, string> colours)
    {
        Dictionary<string, Color> parsed = new(StringComparer.Ordinal);

        foreach ((string key, string value) in colours)
        {
            if (Color.TryParse(value, out Color colour)) parsed[key] = colour;
        }

        return parsed;
    }
}
