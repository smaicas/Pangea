namespace CdCSharp.Pangea.Files.Tests;

/// <summary>A folder of its own among the temporary files, deleted with the test.</summary>
public sealed class Workspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "pangea-files-tests-" + Guid.NewGuid().ToString("N"));

    public Workspace() => Directory.CreateDirectory(Root);

    /// <summary>Writes a file of <paramref name="bytes"/> bytes at a path relative to the root.</summary>
    public string File(string relative, int bytes = 10)
    {
        string path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    public string Folder(string relative)
    {
        string path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
