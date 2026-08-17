namespace CdCSharp.Pangea.Docs.Tests;

/// <summary>Locates the agent skill in the working tree.</summary>
internal static class DocsPaths
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string SkillDirectory => Path.Combine(RepositoryRoot, "tools", "pangea-skill");

    public static string AgentGuide => Path.Combine(SkillDirectory, "SKILL.md");

    public static string ResourceKeyReference =>
        Path.Combine(SkillDirectory, "references", "resource-keys.md");

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
            $"Could not locate CdCSharp.Pangea.sln walking up from '{AppContext.BaseDirectory}'.");
    }
}
