using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using CdCSharp.Pangea.Theming.Palettes;
using CdCSharp.Pangea.Theming.Services;

namespace CdCSharp.Pangea.Theming.Tests;

/// <summary>
/// A density is a property of the theme, so an application driven by a thumb is a constructor
/// argument rather than a style override per control.
/// </summary>
public class TouchMetricsTests
{
    /// <summary>Everything a finger lands on, and the floor Material and Apple both settled near.</summary>
    private static readonly string[] TapTargets =
    [
        "ButtonMinHeight", "TextBoxMinHeight", "ComboBoxMinHeight", "ComboBoxItemMinHeight",
        "ListBoxItemMinHeight", "MenuItemMinHeight", "TabItemMinHeight", "CalendarButtonSize"
    ];

    [Fact]
    public void Touch_CoversEveryMetric()
    {
        List<string> missing = ThemeMetrics.Values.Keys
            .Where(key => !ThemeMetrics.Touch.ContainsKey(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        // A density with a hole in it renders that control unstyled, and the hole would be a metric
        // added to the defaults and not thought about here.
        Assert.True(missing.Count == 0,
            "The touch metrics are missing keys the defaults declare: " + string.Join(", ", missing));
    }

    [Fact]
    public void Touch_PutsEveryTapTargetAtLeastAt48()
    {
        List<string> tooSmall = TapTargets
            .Where(key => (double)ThemeMetrics.Touch[key] < 48d)
            .ToList();

        Assert.True(tooSmall.Count == 0,
            "These touch targets are under 48: " + string.Join(", ", tooSmall));
    }

    [Fact]
    public void Resize_StartsFromTheDefaults()
    {
        IReadOnlyDictionary<string, object> resized =
            ThemeMetrics.Resize(new Dictionary<string, object> { ["ButtonMinHeight"] = 64d });

        Assert.Equal(64d, resized["ButtonMinHeight"]);
        Assert.Equal(ThemeMetrics.Values["ComboBoxMinHeight"], resized["ComboBoxMinHeight"]);
        Assert.Equal(ThemeMetrics.Values.Count, resized.Count);
    }

    [Fact]
    public void ADefaultTheme_KeepsThePointerMetrics()
    {
        PangeaTheme theme = new(new LightPalette(), new DarkPalette());

        Assert.Same(ThemeMetrics.Values, theme.Metrics);
    }

    /// <summary>
    /// The end of the chain: a theme built with the touch metrics lays a real control out at the
    /// touch size, with nothing else asked of the application.
    /// </summary>
    [AvaloniaFact]
    public void AThemeBuiltWithTouchMetrics_LaysControlsOutAtTheTouchSize()
    {
        ThemeService themes = new();
        themes.RegisterTheme("Touch", new PangeaTheme(new LightPalette(), new DarkPalette(), ThemeMetrics.Touch));
        themes.SetTheme("Touch");
        themes.SetVariant(ThemeVariant.Light);

        ComboBox combo = new();
        Button button = new();

        Window window = new() { Width = 400, Height = 300, Content = new StackPanel { Children = { combo, button } } };
        window.Show();
        window.Measure(new Size(400, 300));
        window.Arrange(new Rect(0, 0, 400, 300));

        Assert.True(combo.Bounds.Height >= 48, $"ComboBox laid out at {combo.Bounds.Height}.");
        Assert.True(button.Bounds.Height >= 48, $"Button laid out at {button.Bounds.Height}.");
    }
}
