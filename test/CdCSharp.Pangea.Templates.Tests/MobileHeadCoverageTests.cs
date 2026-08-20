using System.Text.RegularExpressions;

namespace CdCSharp.Pangea.Templates.Tests;

/// <summary>
/// Keeps the check that compiles the platform heads honest.
/// </summary>
/// <remarks>
/// <para>
/// The heads cannot be compiled by the ordinary solution: they are written against the Android and
/// iOS SDKs, which arrive with a workload that not every machine has and the CI image does not
/// carry. <c>CdCSharp.Pangea.Templates.Compile.Mobile</c> compiles them and is opt-in for exactly
/// that reason.
/// </para>
/// <para>
/// Which leaves two ways for it to quietly stop meaning anything: a head is added and the check
/// never learns about it, or the check compiles against a platform minimum the head does not
/// declare - reporting failures the real project does not have, and missing the ones it does. Both
/// are cheap to assert from here, and neither needs a workload.
/// </para>
/// </remarks>
public class MobileHeadCoverageTests
{
    private static string Root { get; } = FindRepositoryRoot();

    private static string MobileCheck =>
        Path.Combine(Root, "test", "CdCSharp.Pangea.Templates.Compile.Mobile",
            "CdCSharp.Pangea.Templates.Compile.Mobile.csproj");

    private static string TemplateContent => Path.Combine(Root, "templates", "content");

    private static IEnumerable<string> HeadProjects(string suffix) =>
        Directory.EnumerateDirectories(TemplateContent, $"*.{suffix}", SearchOption.AllDirectories)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.csproj"));

    private static string? PlatformVersion(string project, string platform)
    {
        string contents = File.ReadAllText(project);

        // The head declares one flat; the check declares one per platform, chosen by a condition.
        Match match = Regex.Match(
            contents,
            platform.Length == 0
                ? @"<SupportedOSPlatformVersion>(?<value>[^<]+)</SupportedOSPlatformVersion>"
                : $@"<SupportedOSPlatformVersion Condition=""[^""]*'{platform}'"">(?<value>[^<]+)</SupportedOSPlatformVersion>");

        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    [Fact]
    public void TheHeadsHaveSomethingThatCompilesThem() =>
        Assert.True(File.Exists(MobileCheck),
            "The platform heads are excluded from Templates.Compile because a net10.0 project cannot " +
            "build them. Without CdCSharp.Pangea.Templates.Compile.Mobile nothing reads that code at all.");

    [Theory]
    [InlineData("Android", "android")]
    [InlineData("iOS", "ios")]
    public void EveryHeadIsCoveredByTheCheck(string suffix, string platform)
    {
        string[] heads = [.. HeadProjects(suffix)];

        Assert.NotEmpty(heads);

        // The check globs by directory suffix, so a new head is picked up by being named like the
        // others. What it cannot pick up is one that is not.
        foreach (string head in heads)
        {
            string directory = Path.GetFileName(Path.GetDirectoryName(head)!);

            Assert.EndsWith($".{suffix}", directory, StringComparison.Ordinal);
        }

        Assert.NotNull(PlatformVersion(MobileCheck, platform));
    }

    /// <summary>
    /// The minimum a head declares is what the platform analyzer checks its calls against. A check
    /// compiling at a different one is worse than no check: it reports what the real project does
    /// not have and stays quiet about what it does.
    /// </summary>
    [Theory]
    [InlineData("Android", "android")]
    [InlineData("iOS", "ios")]
    public void TheCheckCompilesAtTheSameMinimumTheHeadsDeclare(string suffix, string platform)
    {
        string expected = PlatformVersion(MobileCheck, platform)!;

        foreach (string head in HeadProjects(suffix))
        {
            string? declared = PlatformVersion(head, string.Empty);

            Assert.True(declared is not null, $"'{head}' declares no SupportedOSPlatformVersion.");

            Assert.True(declared == expected,
                $"'{Path.GetFileName(head)}' targets {platform} {declared}, and the compile check uses " +
                $"{expected}. The platform analyzer would be answering a different question in each.");
        }
    }

    /// <summary>
    /// It stays out of the solution on purpose: a machine without the workloads has to be able to
    /// build everything else, and the CI image does not carry them.
    /// </summary>
    [Fact]
    public void TheCheckIsNotInTheSolution()
    {
        string solution = File.ReadAllText(Path.Combine(Root, "CdCSharp.Pangea.sln"));

        Assert.DoesNotContain("Templates.Compile.Mobile", solution, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CdCSharp.Pangea.sln"))) return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
