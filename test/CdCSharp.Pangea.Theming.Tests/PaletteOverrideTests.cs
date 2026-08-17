using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using CdCSharp.Pangea.Theming.Palettes;

namespace CdCSharp.Pangea.Theming.Tests;

/// <summary>
/// What the documentation tells an application to do: inherit a palette, override the colours you
/// want, and every brush derived from them follows.
/// </summary>
/// <remarks>
/// "Derived from" is not one rule but three. Most brushes come from the colour of the same name,
/// thirteen are built by hand from another colour, and four are aliases of another brush. An
/// override that only travels through the first is an override that works in the examples and not
/// in the corners.
/// </remarks>
public class PaletteOverrideTests
{
    private const string Sentinel = "#FF00FF";  // magenta, and in no stock palette

    private static Color SentinelColour => Color.Parse(Sentinel);

    /// <summary>Overrides a colour that the regular naming rule turns into a brush.</summary>
    private sealed class AccentOverridden : PangeaPalette
    {
        public override Color ThemeAccentColor => SentinelColour;
    }

    /// <summary>Overrides a colour that thirteen hand-built brushes are derived from.</summary>
    private sealed class ForegroundOverridden : PangeaPalette
    {
        public override Color ThemeForegroundColor => SentinelColour;
    }

    /// <summary>Overrides the colour that an alias ultimately points at.</summary>
    private sealed class ForegroundLowOverridden : PangeaPalette
    {
        public override Color ThemeForegroundLowColor => SentinelColour;
    }

    private sealed class OpaquePickers : PangeaPalette
    {
        public override double PickerHighlightOpacity => 1d;
    }

    /// <summary>Inheriting the dark palette, which the documentation offers as the starting point.</summary>
    private sealed class DarkerStill : DarkPalette
    {
        public override Color ThemeBackgroundColor => SentinelColour;
    }

    private static Color ColourOf(ResourceDictionary resources, string key) => resources[key] switch
    {
        Color colour => colour,
        ISolidColorBrush brush => brush.Color,
        object other => throw new InvalidOperationException($"'{key}' is a {other.GetType().Name}."),
        _ => throw new InvalidOperationException($"'{key}' is missing.")
    };

    [Fact]
    public void AnOverriddenColour_ReachesTheBrushOfTheSameName()
    {
        ResourceDictionary resources = PangeaTheme.BuildVariant(new AccentOverridden());

        Assert.Equal(SentinelColour, ColourOf(resources, "ThemeAccentColor"));
        Assert.Equal(SentinelColour, ColourOf(resources, "ThemeAccentBrush"));
    }

    /// <summary>
    /// <c>RefreshVisualizerForeground</c> is built by hand from <c>ThemeForegroundColor</c>, so the
    /// naming rule never sees it.
    /// </summary>
    [Fact]
    public void AnOverriddenColour_ReachesBrushesBuiltByHandFromIt()
    {
        ResourceDictionary resources = PangeaTheme.BuildVariant(new ForegroundOverridden());

        Assert.Equal(SentinelColour, ColourOf(resources, "RefreshVisualizerForeground"));
    }

    /// <summary>
    /// <c>ThemeDisabledColor</c> is an alias of <c>ThemeForegroundLowColor</c>: two names, and an
    /// override has to be visible through both.
    /// </summary>
    [Fact]
    public void AnOverriddenColour_ReachesItsAliases()
    {
        ResourceDictionary resources = PangeaTheme.BuildVariant(new ForegroundLowOverridden());

        Assert.Equal(SentinelColour, ColourOf(resources, "ThemeForegroundLowColor"));
        Assert.Equal(SentinelColour, ColourOf(resources, "ThemeDisabledColor"));
    }

    /// <summary>The one knob on a palette that is not a colour.</summary>
    [Fact]
    public void OverridingThePickerHighlightOpacity_ChangesThePickerHighlights()
    {
        ResourceDictionary stock = PangeaTheme.BuildVariant(new LightPalette());
        ResourceDictionary overridden = PangeaTheme.BuildVariant(new OpaquePickers());

        double before = ((ISolidColorBrush)stock["DatePickerFlyoutPresenterHighlightFill"]!).Opacity;
        double after = ((ISolidColorBrush)overridden["DatePickerFlyoutPresenterHighlightFill"]!).Opacity;

        Assert.NotEqual(before, after);
        Assert.Equal(1d, after);
        Assert.Equal(1d, ((ISolidColorBrush)overridden["TimePickerFlyoutPresenterHighlightFill"]!).Opacity);
    }

    /// <summary>
    /// The documentation offers <c>DarkPalette</c> as a base too. It already overrides most of the
    /// palette, so a subclass has to win over an override rather than over a default.
    /// </summary>
    [Fact]
    public void InheritingTheDarkPalette_LetsASubclassOverrideWin()
    {
        ResourceDictionary dark = PangeaTheme.BuildVariant(new DarkPalette());
        ResourceDictionary darker = PangeaTheme.BuildVariant(new DarkerStill());

        Assert.NotEqual(SentinelColour, ColourOf(dark, "ThemeBackgroundColor"));
        Assert.Equal(SentinelColour, ColourOf(darker, "ThemeBackgroundColor"));
        Assert.Equal(SentinelColour, ColourOf(darker, "ThemeBackgroundBrush"));

        // Everything it did not override still comes from the dark palette.
        Assert.Equal(ColourOf(dark, "ThemeAccentColor"), ColourOf(darker, "ThemeAccentColor"));
    }

    [Fact]
    public void OverridingOneVariant_LeavesTheOtherAlone()
    {
        PangeaTheme theme = new(new LightPalette(), new DarkerStill());
        ResourceDictionary built = theme.Build();

        ResourceDictionary light = (ResourceDictionary)built.ThemeDictionaries[ThemeVariant.Light];
        ResourceDictionary dark = (ResourceDictionary)built.ThemeDictionaries[ThemeVariant.Dark];

        Assert.Equal(SentinelColour, ColourOf(dark, "ThemeBackgroundColor"));
        Assert.NotEqual(SentinelColour, ColourOf(light, "ThemeBackgroundColor"));
    }
}
