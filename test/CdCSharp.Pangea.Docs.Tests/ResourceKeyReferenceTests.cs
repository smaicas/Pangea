using Avalonia.Media;
using CdCSharp.Pangea.Theming.Palettes;
using System.Reflection;
using System.Text;

namespace CdCSharp.Pangea.Docs.Tests;

/// <summary>
/// Keeps <c>docs/pangea-resource-keys.md</c> in step with the palette.
/// </summary>
/// <remarks>
/// The key list is long, mechanical and changes whenever a colour is added, so it is derived from
/// the palette rather than maintained by hand. Run the suite with PANGEA_UPDATE_DOCS=1 to rewrite it.
/// </remarks>
public class ResourceKeyReferenceTests
{
    private const string UpdateSwitch = "PANGEA_UPDATE_DOCS";

    [Fact]
    public void TheGeneratedReferenceIsUpToDate()
    {
        string expected = BuildReference();

        if (Environment.GetEnvironmentVariable(UpdateSwitch) is "1" or "true")
        {
            File.WriteAllText(DocsPaths.ResourceKeyReference, expected);
            return;
        }

        Assert.True(File.Exists(DocsPaths.ResourceKeyReference),
            $"'{DocsPaths.ResourceKeyReference}' is missing. Regenerate it with {UpdateSwitch}=1.");

        string actual = File.ReadAllText(DocsPaths.ResourceKeyReference).Replace("\r\n", "\n");

        Assert.True(expected.Replace("\r\n", "\n") == actual,
            $"The resource key reference no longer matches the palette. Regenerate it with {UpdateSwitch}=1.");
    }

    [Fact]
    public void EveryColourProducesAMatchingBrush()
    {
        // The rule the guide states: a colour's brush is its name with Color swapped for Brush.
        Avalonia.Controls.ResourceDictionary built = PangeaTheme.BuildVariant(new LightPalette());

        List<string> missing = ColourProperties()
            .Select(property => property.Name.Replace("Color", "Brush"))
            .Where(brushKey => !built.ContainsKey(brushKey))
            .ToList();

        Assert.True(missing.Count == 0, "Colours without a derived brush: " + string.Join(", ", missing));
    }

    private static string BuildReference()
    {
        Avalonia.Controls.ResourceDictionary light = PangeaTheme.BuildVariant(new LightPalette());
        Avalonia.Controls.ResourceDictionary dark = PangeaTheme.BuildVariant(new DarkPalette());

        List<string> colours = ColourProperties().Select(property => property.Name).ToList();

        List<string> brushes = light.Keys.OfType<string>()
            .Where(key => !colours.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        StringBuilder page = new();
        page.Append("""
            # Pangea resource keys

            Generated from the palette by `CdCSharp.Pangea.Docs.Tests`. Do not edit by hand.

            Use these with `{DynamicResource Key}` in XAML. Override a colour by inheriting
            `PangeaPalette` (or `DarkPalette`) and overriding the property of the same name; every
            brush derived from it follows.


            """.Replace("\r\n", "\n"));

        page.Append($"## Colours ({colours.Count})\n\n");
        page.Append("| Key | Light | Dark |\n|---|---|---|\n");

        foreach (string key in colours)
        {
            page.Append($"| `{key}` | `{Describe(light[key])}` | `{Describe(dark[key])}` |\n");
        }

        page.Append($"\n## Brushes and aliases ({brushes.Count})\n\n");
        page.Append("Derived from the colours above; listed so XAML can be checked against them.\n\n");

        foreach (string key in brushes)
        {
            page.Append($"- `{key}`\n");
        }

        page.Append($"\n## Metrics ({ThemeMetrics.Values.Count})\n\n");
        page.Append("Identical in both variants.\n\n| Key | Value |\n|---|---|\n");

        foreach (KeyValuePair<string, object> metric in ThemeMetrics.Values)
        {
            page.Append($"| `{metric.Key}` | `{metric.Value}` |\n");
        }

        return page.ToString();
    }

    private static IEnumerable<PropertyInfo> ColourProperties() =>
        typeof(PangeaPalette)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(Color));

    private static string Describe(object? value) => value switch
    {
        Color colour => colour.ToString(),
        ISolidColorBrush brush => brush.Opacity < 1d ? $"{brush.Color} @{brush.Opacity:0.##}" : brush.Color.ToString(),
        _ => value?.ToString() ?? "<null>"
    };
}
