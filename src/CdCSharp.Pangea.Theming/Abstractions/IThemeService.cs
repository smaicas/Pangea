using Avalonia.Styling;
using CdCSharp.Pangea.Theming.Palettes;

namespace CdCSharp.Pangea.Theming.Abstractions;

/// <summary>
/// Owns the application's appearance along two independent axes.
/// </summary>
/// <remarks>
/// The <em>theme</em> is a pair of palettes, one light and one dark. The <em>variant</em> is which
/// of the two is showing. The two are independent: switching theme keeps the variant, and switching
/// variant keeps the theme.
/// </remarks>
public interface IThemeService
{
    string CurrentTheme { get; }

    ThemeVariant CurrentVariant { get; }

    IReadOnlyCollection<string> AvailableThemes { get; }

    void RegisterTheme(string name, PangeaTheme theme);

    /// <summary>Swaps the palettes in use. The variant is unaffected.</summary>
    void SetTheme(string name);

    /// <summary>Switches between the current theme's light and dark palettes.</summary>
    void SetVariant(ThemeVariant variant);
}
