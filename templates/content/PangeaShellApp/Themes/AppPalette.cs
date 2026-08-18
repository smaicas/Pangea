using Avalonia.Media;
using CdCSharp.Pangea.Theming.Palettes;

namespace PangeaShellApp.Themes;

/// <summary>
/// A theme is a pair of palettes. Override only the colours you want to change: every brush
/// derived from a colour follows it.
/// </summary>
/// <remarks>
/// Registered from <see cref="App.Configure"/>, which replaces the toolkit's default theme with
/// this one and leaves everything else in place.
/// </remarks>
public sealed class AppLightPalette : PangeaPalette
{
    public override Color ThemeAccentColor => Color.Parse("#FF1B6EC2");
}

public sealed class AppDarkPalette : DarkPalette
{
    public override Color ThemeAccentColor => Color.Parse("#FF4F9DDE");
}
