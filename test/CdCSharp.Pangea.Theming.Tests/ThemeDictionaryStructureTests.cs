using CdCSharp.Pangea.Theming.Palettes;
using CdCSharp.Pangea.Theming.Tests.Infrastructure;

namespace CdCSharp.Pangea.Theming.Tests;

/// <summary>
/// L1 - structural checks over the theme XAML. No Avalonia runtime involved, so these run in
/// milliseconds and pinpoint the file at fault. This is the layer that catches a resource key
/// referenced but never defined.
/// </summary>
public class ThemeDictionaryStructureTests
{
    [Fact]
    public void EveryReferencedResourceKey_IsDefinedSomewhereInTheTheme()
    {
        // Keys now come from two places: the XAML that is left, and the palette classes.
        HashSet<string> defined = [.. ThemeSources.AllDefinedKeys()];
        defined.UnionWith(PangeaTheme.BuildVariant(new LightPalette()).Keys.OfType<string>());
        defined.UnionWith(ThemeMetrics.Values.Keys);

        List<string> unresolved = ThemeSources.KeyUsagesByFile()
            .Where(usage => !defined.Contains(usage.Key))
            .Select(usage => $"{usage.Key} <- {string.Join(", ", usage.Value.Distinct().OrderBy(f => f))}")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        Assert.True(unresolved.Count == 0,
            "These resource keys are referenced but never defined, so the DynamicResource silently " +
            "falls back and the control renders unstyled:" + Environment.NewLine +
            string.Join(Environment.NewLine, unresolved));
    }

    [Fact]
    public void Aggregator_IncludesEveryControlDictionary()
    {
        string aggregator = Path.Combine(ThemeSources.SharedDirectory, "SimpleControls.xaml");

        HashSet<string> included = ThemeSources.IncludeSources(ThemeSources.Read(aggregator))
            .Select(source => source.Split('/')[^1])
            .ToHashSet(StringComparer.Ordinal);

        List<string> onDisk = ThemeSources.ControlDictionaries()
            .Select(Path.GetFileName)
            .Where(name => name != "SimpleControls.xaml")
            .ToList()!;

        List<string> notIncluded = onDisk.Where(name => !included.Contains(name!)).ToList();

        Assert.True(notIncluded.Count == 0,
            "Control dictionaries exist on disk but SimpleControls.xaml never merges them, so their " +
            "control themes are dead weight: " + string.Join(", ", notIncluded));
    }

    [Fact]
    public void Aggregator_DoesNotReferenceMissingFiles()
    {
        string aggregator = Path.Combine(ThemeSources.SharedDirectory, "SimpleControls.xaml");

        List<string> dangling = ThemeSources.IncludeSources(ThemeSources.Read(aggregator))
            .Select(source => source.Split('/')[^1])
            .Where(name => !File.Exists(Path.Combine(ThemeSources.SharedDirectory, name)))
            .ToList();

        Assert.True(dangling.Count == 0,
            "SimpleControls.xaml merges dictionaries that no longer exist, which throws at startup: " +
            string.Join(", ", dangling));
    }

    [Fact]
    public void NoDictionary_StillPointsAtTheUpstreamAvaloniaAssembly()
    {
        List<string> leaked = ThemeSources.AllThemeFiles()
            .Where(file => ThemeSources.Read(file).Contains("avares://Avalonia.Themes.", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList()!;

        Assert.True(leaked.Count == 0,
            "These files still resolve resources from Avalonia's own theme assembly instead of this one, " +
            "so edits here would have no effect: " + string.Join(", ", leaked));
    }

    [Fact]
    public void EntryPoint_MergesTheThemeWideDictionaries()
    {
        List<string> includes = ThemeSources.IncludeSources(ThemeSources.Read(ThemeSources.EntryPointFile))
            .Select(source => source.Split('/')[^1])
            .ToList();

        // Root holds the invariant sizing, InvariantResources the invariant strings the control
        // dictionaries bind to, and SimpleControls pulls in every control theme.
        Assert.Contains("Root.axaml", includes);
        Assert.Contains("InvariantResources.axaml", includes);
        Assert.Contains("SimpleControls.xaml", includes);
    }



    [Fact]
    public void NoDictionary_DefinesTheSameKeyTwice()
    {
        List<string> offenders = new();

        foreach (string file in ThemeSources.AllThemeFiles())
        {
            List<string> duplicated = ThemeSources.DefinedKeyOccurrences(ThemeSources.Read(file))
                .Where(entry => entry.Value > 1)
                .Select(entry => entry.Key)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            if (duplicated.Count > 0)
            {
                offenders.Add($"{Path.GetFileName(file)}: {string.Join(", ", duplicated)}");
            }
        }

        // A ResourceDictionary throws on a duplicate key at load time, taking the whole app with it.
        Assert.True(offenders.Count == 0,
            "These dictionaries declare the same key more than once:" + Environment.NewLine +
            string.Join(Environment.NewLine, offenders));
    }

}
