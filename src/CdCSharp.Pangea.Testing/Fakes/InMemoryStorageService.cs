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

    /// <summary>
    /// Reads and converts, and fails the way the real service fails.
    /// </summary>
    /// <remarks>
    /// A double whose failures have different types from the thing it stands in for is worse than
    /// no double: the code under test catches what it was written to catch, the test passes, and
    /// production takes the other branch.
    /// </remarks>
    public async Task<T?> ReadJsonAsync<T>(string filePath) where T : class
    {
        string json = await ReadTextAsync(filePath);

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new StorageSerializationException(
                $"The contents of '{filePath}' are not a {typeof(T).Name}.", filePath, typeof(T), ex);
        }
    }

    /// <inheritdoc cref="ReadJsonAsync{T}"/>
    public Task WriteJsonAsync<T>(string filePath, T data) where T : class
    {
        string json;

        try
        {
            json = JsonSerializer.Serialize(data);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new StorageSerializationException(
                $"A {typeof(T).Name} could not be turned into JSON for '{filePath}', so nothing was written.",
                filePath, typeof(T), ex);
        }

        return WriteTextAsync(filePath, json);
    }

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
