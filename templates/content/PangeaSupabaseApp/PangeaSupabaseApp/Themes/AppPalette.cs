using Avalonia.Media;
using CdCSharp.Pangea.Theming.Palettes;

namespace PangeaSupabaseApp.Themes;

/// <summary>
/// The application's colours.
/// </summary>
/// <remarks>
/// A theme is a pair of palettes. Override only what you want to change: every brush derived from
/// a colour follows it, so one accent here restyles every button, selection and highlight.
/// <para>
/// Never edit the theme XAML under the toolkit's <c>Resources/</c>. Declare a palette instead.
/// </para>
/// </remarks>
public sealed class AppLightPalette : PangeaPalette
{
    public override Color ThemeAccentColor => Color.Parse("#FF1B6EC2");
}

public sealed class AppDarkPalette : DarkPalette
{
    public override Color ThemeAccentColor => Color.Parse("#FF4F9DDE");
}
