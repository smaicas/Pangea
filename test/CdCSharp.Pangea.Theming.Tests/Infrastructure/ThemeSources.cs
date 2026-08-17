using System.Text.RegularExpressions;

namespace CdCSharp.Pangea.Theming.Tests.Infrastructure;

/// <summary>
/// Locates the theme's XAML on disk and pulls the few facts the structural tests need out of it.
/// Reading the sources (rather than the compiled resources) is deliberate: these tests are about
/// keeping the vendored dictionaries coherent, which is a property of the files themselves.
/// </summary>
public static class ThemeSources
{
    private const string ThemingProject = "src/CdCSharp.Pangea.Theming";

    private static readonly Regex DefinedKeyPattern = new(@"x:Key=""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex InlineUsePattern =
        new(@"\{(?:DynamicResource|StaticResource)\s+([^}\s]+)\}", RegexOptions.Compiled);

    private static readonly Regex ElementUsePattern =
        new(@"<(?:DynamicResource|StaticResource)[^>]*?ResourceKey=""([^""]+)""", RegexOptions.Compiled);

    private static readonly Regex IncludePattern =
        new(@"<(?:MergeResourceInclude|StyleInclude|ResourceInclude)[^>]*?Source=""([^""]+)""", RegexOptions.Compiled);

    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string SharedDirectory => Path.Combine(RepositoryRoot, ThemingProject, "Resources", "Controls", "Shared");

    public static string ThemesDirectory => Path.Combine(RepositoryRoot, ThemingProject, "Resources", "Themes");

    public static string EntryPointFile => Path.Combine(RepositoryRoot, ThemingProject, "PangeaUI.axaml");

    /// <summary>Control dictionaries vendored from the Avalonia Simple theme.</summary>
    public static IReadOnlyList<string> ControlDictionaries() =>
        Directory.GetFiles(SharedDirectory, "*.xaml").OrderBy(f => f, StringComparer.Ordinal).ToList();

    /// <summary>Every XAML file that ends up merged into the toolkit theme.</summary>
    public static IReadOnlyList<string> AllThemeFiles() =>
        ControlDictionaries()
            .Concat(Directory.GetFiles(ThemesDirectory, "*.axaml").OrderBy(f => f, StringComparer.Ordinal))
            .Append(EntryPointFile)
            .ToList();

    public static string Read(string path) => File.ReadAllText(path);

    public static IReadOnlySet<string> DefinedKeys(string xaml) =>
        DefinedKeyPattern.Matches(xaml).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    /// <summary>How many times each key is declared in a single file.</summary>
    public static IReadOnlyDictionary<string, int> DefinedKeyOccurrences(string xaml) =>
        DefinedKeyPattern.Matches(xaml)
            .Select(m => m.Groups[1].Value)
            .GroupBy(key => key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    public static IReadOnlySet<string> UsedKeys(string xaml) =>
        InlineUsePattern.Matches(xaml).Select(m => m.Groups[1].Value)
            .Concat(ElementUsePattern.Matches(xaml).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<string> IncludeSources(string xaml) =>
        IncludePattern.Matches(xaml).Select(m => m.Groups[1].Value).ToList();

    /// <summary>
    /// Keys defined across every dictionary. They all merge into one resource scope at runtime,
    /// so a key defined in one file legitimately satisfies a reference in another.
    /// </summary>
    public static IReadOnlySet<string> AllDefinedKeys() =>
        AllThemeFiles().SelectMany(f => DefinedKeys(Read(f))).ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> KeyUsagesByFile()
    {
        Dictionary<string, List<string>> usages = new(StringComparer.Ordinal);

        foreach (string file in AllThemeFiles())
        {
            foreach (string key in UsedKeys(Read(file)))
            {
                if (!usages.TryGetValue(key, out List<string>? files))
                {
                    usages[key] = files = new List<string>();
                }

                files.Add(Path.GetFileName(file));
            }
        }

        return usages.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<string>)kvp.Value, StringComparer.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CdCSharp.Pangea.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate CdCSharp.Pangea.sln walking up from '{AppContext.BaseDirectory}'. " +
            "These tests read the theme XAML from the working tree.");
    }
}
