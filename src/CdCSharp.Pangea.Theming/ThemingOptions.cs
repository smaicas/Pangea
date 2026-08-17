using Avalonia.Styling;
using CdCSharp.Pangea.Theming.Palettes;

namespace CdCSharp.Pangea.Theming;

public class ThemingOptions
{
    public static ThemingOptions Default => new();

    /// <summary>
    /// Themes available to the application, by name, pre-populated with the toolkit's own.
    /// </summary>
    /// <remarks>
    /// Replace the entry to restyle the application, keeping everything else:
    /// <code>options.Themes[PangeaTheme.DefaultName] = new PangeaTheme(new MyLight(), new MyDark());</code>
    /// Add entries instead to offer a choice of themes.
    /// </remarks>
    public Dictionary<string, PangeaTheme> Themes { get; } = new()
    {
        [PangeaTheme.DefaultName] = PangeaTheme.Default
    };

    /// <summary>Theme to start on. Null selects <see cref="PangeaTheme.DefaultName"/>.</summary>
    public string? DefaultTheme { get; set; }

    /// <summary>Start on the platform's light or dark preference.</summary>
    public bool EnableSystemThemeDetection { get; set; } = true;

    /// <summary>Variant to start on when detection is off or the platform has no preference.</summary>
    public ThemeVariant FallbackVariant { get; set; } = ThemeVariant.Dark;
}
