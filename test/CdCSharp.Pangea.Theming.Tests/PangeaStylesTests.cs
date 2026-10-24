using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using CdCSharp.Pangea.Theming.Tests.Infrastructure;

namespace CdCSharp.Pangea.Theming.Tests;

/// <summary>
/// The opt-in classes: every colour they use exists in both variants, every icon is a shape, and a
/// class reaches a control that asks for it - and only that one.
/// </summary>
public class PangeaStylesTests
{
    private static string StylesFile => Path.Combine(ThemeSources.RepositoryRoot, "src", "CdCSharp.Pangea.Theming", "PangeaStyles.axaml");

    private static string IconsFile => Path.Combine(ThemeSources.RepositoryRoot, "src", "CdCSharp.Pangea.Theming", "Resources", "Icons", "PangeaIcons.axaml");

    public static TheoryData<string> VariantNames() => new("Light", "Dark");

    [AvaloniaTheory]
    [MemberData(nameof(VariantNames))]
    public void EveryResourceTheClassesUse_ResolvesInTheVariant(string variantName)
    {
        ThemeVariant variant = ThemeHarness.Variant(variantName);

        List<string> unresolved = ThemeSources.UsedKeys(ThemeSources.Read(StylesFile))
            .Where(key => !ThemeHarness.TryResolve(key, variant, out object? value) || value is null)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(unresolved.Count == 0, $"PangeaStyles uses keys '{variantName}' does not define: " + string.Join(", ", unresolved));
    }

    [AvaloniaFact]
    public void EveryIcon_IsANamedShape()
    {
        PangeaStyles styles = new();
        IReadOnlySet<string> keys = ThemeSources.DefinedKeys(ThemeSources.Read(IconsFile));

        Assert.True(keys.Count >= 40, $"Only {keys.Count} icons; the dictionary is not being read.");

        foreach (string key in keys)
        {
            Assert.StartsWith("Icon.", key, StringComparison.Ordinal);
            Assert.True(styles.TryGetResource(key, ThemeVariant.Default, out object? value), $"'{key}' is not reachable through PangeaStyles.");

            Geometry geometry = Assert.IsAssignableFrom<Geometry>(value);
            Assert.True(geometry.Bounds.Width > 0 && geometry.Bounds.Height > 0, $"'{key}' draws nothing.");
        }
    }

    [AvaloniaFact]
    public void AClass_ReachesTheControlThatAsks_AndNoOther()
    {
        PangeaStyles styles = new();
        Application.Current!.Styles.Add(styles);

        try
        {
            Border card = new() { Classes = { "card" } };
            Border plain = new();
            TextBlock hint = new() { Classes = { "hint" }, Text = "A hint" };
            Window window = new() { Content = new StackPanel { Children = { card, plain, hint } } };

            window.Show();

            Assert.Equal(new CornerRadius(8), card.CornerRadius);
            Assert.Equal(new Thickness(12), card.Padding);
            Assert.Equal(new CornerRadius(0), plain.CornerRadius);
            Assert.Equal(TextWrapping.Wrap, hint.TextWrapping);

            Assert.True(ThemeHarness.TryResolve("ThemeForegroundLowBrush", ThemeVariant.Light, out object? low));
            Assert.Equal(((ISolidColorBrush)low!).Color, ((ISolidColorBrush)hint.Foreground!).Color);

            window.Close();
        }
        finally
        {
            Application.Current.Styles.Remove(styles);
        }
    }

    [AvaloniaFact]
    public void AnApplicationsOwnIcon_WinsOverTheToolkits()
    {
        PangeaStyles styles = new();
        Application.Current!.Styles.Add(styles);
        StreamGeometry own = StreamGeometry.Parse("M0,0L10,10");
        Application.Current.Resources["Icon.Plus"] = own;

        try
        {
            Assert.True(Application.Current.TryGetResource("Icon.Plus", ThemeVariant.Default, out object? value));
            Assert.Same(own, value);
        }
        finally
        {
            Application.Current.Resources.Remove("Icon.Plus");
            Application.Current.Styles.Remove(styles);
        }
    }
}
