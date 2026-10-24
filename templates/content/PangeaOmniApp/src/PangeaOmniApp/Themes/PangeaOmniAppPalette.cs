using Avalonia.Media;
using CdCSharp.Pangea.Theming.Palettes;

namespace PangeaOmniApp.Themes;

/// <summary>
/// The application's own colours, used while it runs alone. Override the colours to change;
/// every brush derived from them follows.
/// </summary>
public sealed class PangeaOmniAppLightPalette : PangeaPalette
{
    public override Color ThemeAccentColor => Color.Parse("#FF2563EB");
    public override Color ThemeAccentLightColor => Color.Parse("#FF3B82F6");
    public override Color ThemeAccentDarkColor => Color.Parse("#FF1D4ED8");

    public override Color SelectionColor => Color.Parse("#FF2563EB");
    public override Color SelectionBackgroundColor => Color.Parse("#332563EB");
    public override Color SelectionHoverColor => Color.Parse("#662563EB");
    public override Color HighlightColor => Color.Parse("#FF2563EB");
}

public sealed class PangeaOmniAppDarkPalette : DarkPalette
{
    public override Color ThemeAccentColor => Color.Parse("#FF60A5FA");
    public override Color ThemeAccentLightColor => Color.Parse("#FF93C5FD");
    public override Color ThemeAccentDarkColor => Color.Parse("#FF3B82F6");

    public override Color SelectionColor => Color.Parse("#FF60A5FA");
    public override Color SelectionBackgroundColor => Color.Parse("#3360A5FA");
    public override Color SelectionHoverColor => Color.Parse("#6660A5FA");
    public override Color HighlightColor => Color.Parse("#FF60A5FA");
}
