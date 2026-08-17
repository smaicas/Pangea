using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using CdCSharp.Pangea.Theming.Palettes;
using CdCSharp.Pangea.Theming.Tests.Infrastructure;

namespace CdCSharp.Pangea.Theming.Tests;

/// <summary>
/// L2 - the resource contract, asked of a running application. This is the layer that catches a
/// palette missing a key the control dictionaries bind to, per variant.
/// </summary>
public class ThemeResourceResolutionTests
{
    public static TheoryData<string> VariantNames() => new("Default", "Light", "Dark");

    [AvaloniaTheory]
    [MemberData(nameof(VariantNames))]
    public void EveryReferencedKey_ResolvesInEveryVariant(string variantName)
    {
        ThemeVariant variant = ThemeHarness.Variant(variantName);

        List<string> unresolved = ThemeSources.KeyUsagesByFile().Keys
            .Where(key => !ThemeHarness.TryResolve(key, variant, out object? value) || value is null)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(unresolved.Count == 0,
            $"Variant '{variantName}' does not resolve these keys, so anything bound to them renders " +
            "with an unset value: " + string.Join(", ", unresolved));
    }

    [AvaloniaTheory]
    [MemberData(nameof(VariantNames))]
    public void CoreBrushes_ResolveToActualBrushes(string variantName)
    {
        ThemeVariant variant = ThemeHarness.Variant(variantName);

        string[] coreBrushes =
        [
            "ThemeBackgroundBrush", "ThemeForegroundBrush", "ThemeBorderMidBrush",
            "ThemeControlMidBrush", "TitleBarBackgroundBrush"
        ];

        foreach (string key in coreBrushes)
        {
            Assert.True(ThemeHarness.TryResolve(key, variant, out object? value),
                $"'{key}' did not resolve under variant '{variantName}'.");
            Assert.IsAssignableFrom<IBrush>(value);
        }
    }

    [AvaloniaFact]
    public void LightAndDark_ResolveToDifferentValuesForTheSameKey()
    {
        Assert.True(ThemeHarness.TryResolve("ThemeBackgroundBrush", ThemeVariant.Light, out object? light));
        Assert.True(ThemeHarness.TryResolve("ThemeBackgroundBrush", ThemeVariant.Dark, out object? dark));

        Assert.NotEqual(((ISolidColorBrush)light!).Color, ((ISolidColorBrush)dark!).Color);
    }

    [AvaloniaFact]
    public void SwitchingVariant_MovesAvaloniaThemeVariant()
    {
        ThemeHarness.ApplyVariant(ThemeVariant.Dark);
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);

        ThemeHarness.ApplyVariant(ThemeVariant.Light);
        Assert.Equal(ThemeVariant.Light, Application.Current!.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void ThemeVariantScope_RendersASubtreeInTheOppositeVariant()
    {
        ThemeHarness.ApplyVariant(ThemeVariant.Dark);

        // A scope asking for Light must resolve the light palette even though the application is
        // dark. This is the whole point of shipping the palettes as variants.
        ThemeVariantScope scope = new() { RequestedThemeVariant = ThemeVariant.Light };
        Border inner = new();
        scope.Child = inner;

        Window window = new() { Width = 200, Height = 100, Content = scope };
        window.Show();

        Assert.True(inner.TryFindResource("ThemeBackgroundBrush", out object? scoped));
        Assert.True(ThemeHarness.TryResolve("ThemeBackgroundBrush", ThemeVariant.Dark, out object? ambient));

        Assert.NotEqual(((ISolidColorBrush)ambient!).Color, ((ISolidColorBrush)scoped!).Color);
    }

    [AvaloniaFact]
    public void InvariantStrings_AreAvailableToTheControlDictionaries()
    {
        // The control dictionaries bind these directly; without them the flyouts and the scrollbar
        // context menu render with empty labels.
        string[] strings = ["StringTextFlyoutCopyText", "StringScrollBarPageDown", "StringDatePickerDayText"];

        foreach (string key in strings)
        {
            Assert.True(ThemeHarness.TryResolve(key, ThemeVariant.Light, out object? value),
                $"Invariant string '{key}' is missing.");
            Assert.False(string.IsNullOrWhiteSpace(value as string), $"'{key}' resolved to an empty string.");
        }
    }

    [AvaloniaFact]
    public void SelectingAnUnregisteredTheme_Throws() =>
        Assert.Throws<InvalidOperationException>(() => ThemeHarness.Service.SetTheme("NotRegistered"));

    [AvaloniaFact]
    public void SwitchingTheme_ReplacesThePaletteInsteadOfStacking()
    {
        // Touch the service first: it merges the toolkit palette on creation.
        ThemeHarness.Service.RegisterTheme("Alt", new PangeaTheme(new LightPalette(), new DarkPalette()));
        int before = Application.Current!.Resources.MergedDictionaries.Count;

        ThemeHarness.Service.SetTheme("Alt");
        ThemeHarness.Service.SetTheme(PangeaTheme.DefaultName);

        Assert.Equal(before, Application.Current!.Resources.MergedDictionaries.Count);
    }
}
