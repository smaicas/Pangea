using Avalonia.Media;
using CdCSharp.Pangea.Theming.Palettes;

namespace MyPangeaApp.Themes;

/// <summary>
/// A theme is a pair of palettes. Override only the colours you want to change: every brush
/// derived from a colour follows it.
/// </summary>
/// <remarks>
/// Register it from App.Configure:
/// <code>
/// services.Configure&lt;ThemingOptions&gt;(options =>
///     options.Themes[PangeaTheme.DefaultName] = new PangeaTheme(new AppLightPalette(), new AppDarkPalette()));
/// </code>
/// </remarks>
public sealed class AppLightPalette : PangeaPalette
{
    public override Color ThemeAccentColor => Color.Parse("#FF1B6EC2");
}

public sealed class AppDarkPalette : DarkPalette
{
    public override Color ThemeAccentColor => Color.Parse("#FF4F9DDE");
}
