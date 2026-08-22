using System.Text.Json;

namespace CdCSharp.Pangea.Templates.Tests;

/// <summary>
/// The shape of what <c>dotnet new</c> writes out.
/// </summary>
/// <remarks>
/// None of this is checked by compiling the templates' sources, which is what the rest of this
/// suite does: a primaryOutputs path that no longer exists, or a solution naming a project that was
/// moved, only fails for whoever generates a project - and generating one needs the packages
/// published, so it is not something the build can do for itself.
/// </remarks>
public class TemplateLayoutTests
{
    private static string ContentRoot { get; } = FindContentRoot();

    public static TheoryData<string> Templates() =>
        new(Directory.GetDirectories(ContentRoot).Select(Path.GetFileName).OfType<string>());

    [Theory]
    [MemberData(nameof(Templates))]
    public void PrimaryOutputs_NameAProjectThatExists(string template)
    {
        string root = Path.Combine(ContentRoot, template);

        using JsonDocument config = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, ".template.config", "template.json")));

        foreach (JsonElement output in config.RootElement.GetProperty("primaryOutputs").EnumerateArray())
        {
            string path = output.GetProperty("path").GetString()!;

            Assert.True(File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))),
                $"{template} names '{path}' as its primary output, and there is no such file.");
        }
    }

    /// <summary>
    /// Every template ships tests. A generated project with no test project is one where writing
    /// the first test means deciding on a layout, a runner and a set of doubles first, which is how
    /// a project ends up with none.
    /// </summary>
    [Theory]
    [MemberData(nameof(Templates))]
    public void EveryTemplate_ShipsATestProject(string template)
    {
        string tests = Path.Combine(ContentRoot, template, $"{template}.Tests", $"{template}.Tests.csproj");

        Assert.True(File.Exists(tests), $"{template} has no test project at {template}.Tests.");
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void TheSolution_NamesProjectsThatExist(string template)
    {
        string root = Path.Combine(ContentRoot, template);
        string solution = Path.Combine(root, $"{template}.slnx");

        Assert.True(File.Exists(solution), $"{template} has no {template}.slnx.");

        foreach (string line in File.ReadAllLines(solution))
        {
            // The conditional heads are commented out of the build for a template that excludes
            // them, but the path still has to be right for the one that does not.
            int start = line.IndexOf("Path=\"", StringComparison.Ordinal);

            if (start < 0) continue;

            start += "Path=\"".Length;

            string path = line[start..line.IndexOf('"', start)];

            Assert.True(File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))),
                $"{template}.slnx names '{path}', and there is no such project.");
        }
    }

    private static string FindContentRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "templates", "content");

            if (Directory.Exists(candidate)) return candidate;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("templates/content was not found above the test binaries.");
    }
}
