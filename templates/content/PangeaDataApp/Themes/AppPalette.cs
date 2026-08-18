using Avalonia.Media;
using CdCSharp.Pangea.Theming.Palettes;

namespace PangeaDataApp.Themes;

/// <summary>
/// A theme is a pair of palettes. Override only the colours you want to change: every brush
/// derived from a colour follows it.
/// </summary>
public sealed class AppLightPalette : PangeaPalette
{
    public override Color ThemeAccentColor => Color.Parse("#FF0F766E");
}

public sealed class AppDarkPalette : DarkPalette
{
    public override Color ThemeAccentColor => Color.Parse("#FF2DD4BF");
}
