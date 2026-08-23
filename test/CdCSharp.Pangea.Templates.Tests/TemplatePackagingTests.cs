using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CdCSharp.Pangea.Templates.Tests;

/// <summary>
/// What packing does to the templates' configuration on its way into the package.
/// </summary>
/// <remarks>
/// <para>
/// The version a generated project asks for is written into <c>template.json</c> at pack time, and
/// nothing downstream can tell a wrong number from a right one: a template that asks for a version
/// that does not exist produces a project that fails to restore on the machine of whoever generated
/// it, which is the last place anybody wants to find out.
/// </para>
/// <para>
/// It has been wrong. The stamp matched any default that looked like a version number, so the
/// mobile templates' AvaloniaVersion was stamped with Pangea's version too, and generated projects
/// asked for Avalonia 1.0.52.
/// </para>
/// </remarks>
public class TemplatePackagingTests
{
    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    private static string ContentRoot => Path.Combine(RepositoryRoot, "templates", "content");

    /// <summary>The regex the pack target uses, read from the build file rather than restated.</summary>
    /// <remarks>
    /// Restating it would test a copy: the point is that the expression which actually runs during
    /// pack hits one symbol and no other.
    /// </remarks>
    private static string StampPattern
    {
        get
        {
            string csproj = Path.Combine(RepositoryRoot, "templates", "CdCSharp.Pangea.Templates.csproj");

            string? pattern = XDocument.Load(csproj)
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "PangeaVersionDefaultPattern")
                ?.Value;

            Assert.True(pattern is not null,
                "The templates project no longer defines PangeaVersionDefaultPattern, so the stamp is " +
                "matching something this test cannot see.");

            return pattern!;
        }
    }

    public static TheoryData<string> TemplateConfigs()
    {
        TheoryData<string> data = [];

        foreach (string directory in Directory.GetDirectories(ContentRoot))
        {
            data.Add(Path.Combine(directory, ".template.config", "template.json"));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(TemplateConfigs))]
    public void TheVersionStamp_MatchesThePangeaSymbolAndNothingElse(string config)
    {
        MatchCollection matches = Regex.Matches(File.ReadAllText(config), StampPattern);

        Assert.True(matches.Count == 1,
            $"{Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(config)))} has {matches.Count} " +
            "places the version stamp would rewrite; it must be exactly the PangeaVersion symbol.");

        Assert.Contains("PANGEA_VERSION", matches[0].Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// The stamp applied for real, against every other version-shaped default in the file.
    /// </summary>
    [Theory]
    [MemberData(nameof(TemplateConfigs))]
    public void TheVersionStamp_LeavesEveryOtherVersionAlone(string config)
    {
        string original = File.ReadAllText(config);

        string stamped = Regex.Replace(
            original, StampPattern, "\"defaultValue\": \"9.9.9\",$1\"replaces\": \"PANGEA_VERSION\"");

        // Every other default keeps the number it had. Compared as a set of the file's own values,
        // so a symbol added later is covered without this test being told about it.
        List<string> before = OtherDefaults(original);
        List<string> after = OtherDefaults(stamped);

        Assert.Equal(before, after);
        Assert.Contains("\"defaultValue\": \"9.9.9\"", stamped, StringComparison.Ordinal);
    }

    /// <summary>
    /// The stamp is undone after packing. A stamped file committed by mistake would send everyone
    /// who builds from source at whatever version happened to be packed on that machine.
    /// </summary>
    [Theory]
    [MemberData(nameof(TemplateConfigs))]
    public void InSource_ThePangeaVersionDefaultIsTheUnstampedOne(string config)
    {
        Match match = Regex.Match(File.ReadAllText(config), StampPattern);

        Assert.Contains("\"1.0.0\"", match.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// The mobile templates pin Avalonia for every head at once, so the number has to be the one
    /// the toolkit itself is built against - a generated project resolving a different Avalonia to
    /// the one CdCSharp.Pangea demands fails to restore as a downgrade.
    /// </summary>
    [Theory]
    [MemberData(nameof(TemplateConfigs))]
    public void TheAvaloniaDefault_IsTheVersionTheToolkitBuildsAgainst(string config)
    {
        Match declared = Regex.Match(
            File.ReadAllText(config),
            "\"defaultValue\": \"(?<version>[^\"]+)\",\\s*\"replaces\": \"AVALONIA_VERSION\"");

        if (!declared.Success) return; // Desktop templates name Avalonia in the csproj instead.

        Assert.Equal(ToolkitAvaloniaVersion(), declared.Groups["version"].Value);
    }

    /// <summary>Version-shaped defaults other than the Pangea one, in file order.</summary>
    private static List<string> OtherDefaults(string config) =>
        Regex.Matches(config, "\"defaultValue\": \"(?<version>\\d+\\.\\d+\\.\\d+)\",\\s*\"replaces\": \"(?<token>[^\"]+)\"")
            .Where(match => match.Groups["token"].Value != "PANGEA_VERSION")
            .Select(match => $"{match.Groups["token"].Value}={match.Groups["version"].Value}")
            .ToList();

    private static string ToolkitAvaloniaVersion()
    {
        string csproj = Path.Combine(RepositoryRoot, "src", "CdCSharp.Pangea", "CdCSharp.Pangea.csproj");

        string? version = XDocument.Load(csproj)
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .FirstOrDefault(element => (string?)element.Attribute("Include") == "Avalonia")
            ?.Attribute("Version")?.Value;

        Assert.True(version is not null, "CdCSharp.Pangea no longer references Avalonia by version.");

        return version!;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "templates", "content")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("templates/content was not found above the test binaries.");
    }
}
