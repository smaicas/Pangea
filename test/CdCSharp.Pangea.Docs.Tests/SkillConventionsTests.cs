using System.Text.RegularExpressions;

namespace CdCSharp.Pangea.Docs.Tests;

/// <summary>
/// Checks the skill follows the layout agents expect: a <c>SKILL.md</c> whose YAML frontmatter
/// declares what it is and when to reach for it, with bulk material in separate reference files.
/// </summary>
public class SkillConventionsTests
{
    private static string Skill => File.ReadAllText(DocsPaths.AgentGuide);

    private static string Frontmatter
    {
        get
        {
            Match match = Regex.Match(Skill, @"\A---\r?\n(?<body>.*?)\r?\n---\r?\n", RegexOptions.Singleline);
            Assert.True(match.Success, "SKILL.md must open with a YAML frontmatter block delimited by ---.");
            return match.Groups["body"].Value;
        }
    }

    private static string Field(string name)
    {
        Match match = Regex.Match(Frontmatter, $@"^{name}:\s*(?<value>.+)$", RegexOptions.Multiline);
        Assert.True(match.Success, $"The frontmatter is missing '{name}'.");
        return match.Groups["value"].Value.Trim();
    }

    [Fact]
    public void SkillFileExistsWhereAgentsLookForIt() =>
        Assert.True(File.Exists(DocsPaths.AgentGuide), $"Expected a skill at '{DocsPaths.AgentGuide}'.");

    [Fact]
    public void NameIsALowercaseSlugMatchingTheDirectory()
    {
        string name = Field("name");

        Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", name);
        Assert.Equal(Path.GetFileName(DocsPaths.SkillDirectory), $"{name}-skill");
    }

    [Fact]
    public void DescriptionSaysWhatItDoesAndWhenToUseIt()
    {
        string description = Field("description");

        Assert.InRange(description.Length, 60, 1024);

        // An agent picks a skill from its description alone, so it has to carry the trigger.
        Assert.Contains("use when", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pangea", description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReferencedSupportingFilesExist()
    {
        foreach (Match match in Regex.Matches(Skill, @"`(references/[^`]+)`"))
        {
            string relative = match.Groups[1].Value;
            string path = Path.Combine(DocsPaths.SkillDirectory, relative.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(path), $"SKILL.md points at '{relative}', which does not exist.");
        }
    }

    [Fact]
    public void TheEntryPointStaysFocused()
    {
        // Bulk material belongs in references, loaded when needed, not in the file every agent reads.
        int lines = Skill.Split('\n').Length;

        Assert.True(lines < 600, $"SKILL.md is {lines} lines; move reference material into references/.");
    }

    [Fact]
    public void EverySkillFileIsMarkdown()
    {
        List<string> unexpected = Directory
            .EnumerateFiles(DocsPaths.SkillDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetRelativePath(DocsPaths.SkillDirectory, path))
            .ToList();

        Assert.True(unexpected.Count == 0,
            "The skill packages as documentation only: " + string.Join(", ", unexpected));
    }
}
