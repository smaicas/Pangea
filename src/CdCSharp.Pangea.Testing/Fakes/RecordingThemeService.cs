using Avalonia.Styling;
using CdCSharp.Pangea.Theming.Abstractions;
using CdCSharp.Pangea.Theming.Palettes;

namespace CdCSharp.Pangea.Testing.Fakes;

/// <summary>
/// Tracks the theme and variant without touching an application's styles.
/// </summary>
/// <remarks>
/// The real service edits <c>Application.Current.Resources</c>, which needs an application to
/// exist. This keeps the two axes the interface promises - which palettes are in use, and whether
/// the light or dark one is showing - so a settings screen can be tested for what it asked for.
/// </remarks>
public sealed class RecordingThemeService : IThemeService
{
    private readonly Dictionary<string, PangeaTheme> _themes = new(StringComparer.Ordinal)
    {
        [PangeaTheme.DefaultName] = PangeaTheme.Default
    };

    public string CurrentTheme { get; private set; } = PangeaTheme.DefaultName;

    public ThemeVariant CurrentVariant { get; private set; } = ThemeVariant.Light;

    public IReadOnlyCollection<string> AvailableThemes => _themes.Keys;

    /// <summary>Every theme asked for, in order.</summary>
    public List<string> ThemesSet { get; } = [];

    /// <summary>Every variant asked for, in order.</summary>
    public List<ThemeVariant> VariantsSet { get; } = [];

    public void RegisterTheme(string name, PangeaTheme theme)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(theme);

        _themes[name] = theme;
    }

    public void SetTheme(string name)
    {
        if (!_themes.ContainsKey(name))
        {
            throw new ArgumentException($"Theme '{name}' is not registered.", nameof(name));
        }

        ThemesSet.Add(name);
        CurrentTheme = name;
    }

    public void SetVariant(ThemeVariant variant)
    {
        VariantsSet.Add(variant);
        CurrentVariant = variant;
    }
}
