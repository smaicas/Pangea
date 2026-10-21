using Avalonia.Controls;
using Avalonia.Media;
using CdCSharp.Pangea.Theming.Palettes;

namespace CdCSharp.Pangea.Theming.Tests;

/// <summary>
/// A palette that leaves the process as data has to come back as the same theme: that is the whole
/// of what a host imposing its look on other applications relies on.
/// </summary>
public class DictionaryPaletteTests
{
    private sealed class Branded : DarkPalette
    {
        public override Color ThemeAccentColor => Color.Parse("#FF12AB34");

        public override double PickerHighlightOpacity => 0.6d;
    }

    [Fact]
    public void A_palette_read_back_from_its_colours_builds_the_same_resources()
    {
        Branded original = new();
        DictionaryPalette copy = new(original.ToColours(), original.PickerHighlightOpacity);

        ResourceDictionary expected = PangeaTheme.BuildVariant(original);
        ResourceDictionary actual = PangeaTheme.BuildVariant(copy);

        Assert.Equal(expected.Count, actual.Count);

        foreach (object key in expected.Keys)
        {
            Assert.Equal(ColourOf(expected[key]), ColourOf(actual[key]));
        }
    }

    [Fact]
    public void Colours_the_dictionary_lacks_keep_the_default_palette()
    {
        DictionaryPalette partial = new(new Dictionary<string, Color> { ["ThemeAccentColor"] = Colors.Red });
        IReadOnlyDictionary<string, Color> colours = partial.ToColours();

        Assert.Equal(Colors.Red, colours["ThemeAccentColor"]);
        Assert.Equal(new LightPalette().ThemeBackgroundColor, colours["ThemeBackgroundColor"]);
    }

    [Fact]
    public void A_colour_the_toolkit_does_not_know_still_becomes_a_resource_and_a_brush()
    {
        DictionaryPalette extended = new(new Dictionary<string, Color> { ["AppSidebarColor"] = Colors.Teal });

        ResourceDictionary resources = PangeaTheme.BuildVariant(extended);

        Assert.Equal(Colors.Teal, resources["AppSidebarColor"]);
        Assert.Equal(Colors.Teal, Assert.IsAssignableFrom<ISolidColorBrush>(resources["AppSidebarBrush"]).Color);
    }

    private static object? ColourOf(object? resource) => resource switch
    {
        ISolidColorBrush brush => (brush.Color, brush.Opacity),
        _ => resource
    };
}
