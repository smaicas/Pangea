using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using CdCSharp.Pangea.Theming.Abstractions;
using CdCSharp.Pangea.Theming.Palettes;

namespace CdCSharp.Pangea.Theming.Services;

/// <summary>
/// Keeps exactly one theme dictionary merged into the application and moves Avalonia's variant.
/// </summary>
public class ThemeService : IThemeService
{
    private readonly Dictionary<string, PangeaTheme> _themes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    private ResourceDictionary? _appliedResources;

    public ThemeService() => RegisterTheme(PangeaTheme.DefaultName, PangeaTheme.Default);

    public string CurrentTheme { get; private set; } = PangeaTheme.DefaultName;

    public ThemeVariant CurrentVariant { get; private set; } = ThemeVariant.Default;

    public IReadOnlyCollection<string> AvailableThemes
    {
        get
        {
            lock (_lock) return _themes.Keys.ToList();
        }
    }

    public void RegisterTheme(string name, PangeaTheme theme)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(theme);

        lock (_lock) _themes[name] = theme;
    }

    public void SetTheme(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        PangeaTheme theme;
        lock (_lock)
        {
            if (!_themes.TryGetValue(name, out PangeaTheme? registered))
            {
                throw new InvalidOperationException(
                    $"Theme '{name}' is not registered. Available: {string.Join(", ", _themes.Keys)}");
            }

            theme = registered;
            CurrentTheme = name;
        }

        Apply(theme);
    }

    public void SetVariant(ThemeVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);

        CurrentVariant = variant;

        if (Application.Current is { } application) application.RequestedThemeVariant = variant;
    }

    /// <summary>
    /// Replaces the merged palette. Building it once per switch keeps the application holding a
    /// single theme dictionary instead of a growing stack of them.
    /// </summary>
    private void Apply(PangeaTheme theme)
    {
        if (Application.Current?.Resources.MergedDictionaries is not { } merged) return;

        if (_appliedResources is not null) merged.Remove(_appliedResources);

        _appliedResources = theme.Build();
        merged.Add(_appliedResources);
    }
}
