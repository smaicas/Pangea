using CdCSharp.Pangea.Storage.Abstractions;
using System.Text.Json;

namespace CdCSharp.Pangea.Testing.Fakes;

/// <summary>
/// A storage service that keeps everything in memory.
/// </summary>
/// <remarks>
/// The real one writes to the per-user data directory for the platform, which makes a test that
/// uses it leave files behind, share state with the next run, and behave differently on each
/// operating system. This keeps the same shape - the same paths, the same round trip through JSON -
/// with nothing on disk.
/// </remarks>
public sealed class InMemoryStorageService : IStorageService
{
    private const string Root = "/pangea-test";

    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal) { Root };

    /// <summary>Every file written, by path. Readable so a test can assert on what was saved.</summary>
    public IReadOnlyDictionary<string, string> Files => _files;

    public string GetApplicationDataPath() => Root;

    public string GetUserDataPath() => Root + "/user";

    public string GetTempPath() => Root + "/temp";

    public string GetCachePath() => Root + "/cache";

    public string GetDataFilePath(string fileName) => GetApplicationDataPath() + "/" + fileName;

    public Task<string> ReadTextAsync(string filePath) =>
        _files.TryGetValue(filePath, out string? content)
            ? Task.FromResult(content)
            : throw new FileNotFoundException("No such file in the in-memory storage.", filePath);

    public Task WriteTextAsync(string filePath, string content)
    {
        _files[filePath] = content;
        return Task.CompletedTask;
    }

    public async Task<T?> ReadJsonAsync<T>(string filePath) where T : class =>
        JsonSerializer.Deserialize<T>(await ReadTextAsync(filePath));

    public Task WriteJsonAsync<T>(string filePath, T data) where T : class =>
        WriteTextAsync(filePath, JsonSerializer.Serialize(data));

    public bool FileExists(string filePath) => _files.ContainsKey(filePath);

    public bool DirectoryExists(string directoryPath) => _directories.Contains(directoryPath);

    public void CreateDirectory(string directoryPath) => _directories.Add(directoryPath);

    public void DeleteFile(string filePath) => _files.Remove(filePath);

    public void DeleteDirectory(string directoryPath, bool recursive = false)
    {
        _directories.Remove(directoryPath);

        if (!recursive) return;

        string prefix = directoryPath + "/";

        foreach (string path in _files.Keys.Where(path => path.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _files.Remove(path);
        }
    }
}
