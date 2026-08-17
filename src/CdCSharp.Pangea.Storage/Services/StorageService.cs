using CdCSharp.Pangea.Storage.Abstractions;
using System.Text.Json;

namespace CdCSharp.Pangea.Storage.Services;

/// <summary>
/// File access rooted at the platform's per-application folders.
/// </summary>
/// <remarks>
/// Writes create the folders they need; reads do not touch the filesystem beyond reading. Having
/// <c>ReadTextAsync</c> create directories was a surprise with real consequences: asking for a file
/// that is not there left an empty folder behind.
/// </remarks>
public class StorageService : IStorageService
{
    private readonly IPlatformPathProvider _pathProvider;
    private readonly JsonSerializerOptions _jsonOptions;

    public StorageService(IPlatformPathProvider pathProvider)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public string GetApplicationDataPath() => _pathProvider.GetApplicationDataPath();

    public string GetUserDataPath() => _pathProvider.GetUserDataPath();

    public string GetTempPath() => _pathProvider.GetTempPath();

    public string GetCachePath() => _pathProvider.GetCachePath();

    public string GetDataFilePath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return Path.Combine(GetApplicationDataPath(), fileName);
    }

    /// <summary>
    /// Reads a file, with the same semantics as <see cref="File.ReadAllTextAsync(string, CancellationToken)"/>:
    /// a missing file throws <see cref="FileNotFoundException"/>, a missing folder
    /// <see cref="DirectoryNotFoundException"/>.
    /// </summary>
    public Task<string> ReadTextAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return File.ReadAllTextAsync(filePath);
    }

    /// <summary>Writes a file, creating its folder if needed.</summary>
    public Task WriteTextAsync(string filePath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(content);

        EnsureDirectoryExists(filePath);
        return File.WriteAllTextAsync(filePath, content);
    }

    /// <summary>
    /// Reads and deserializes a file, returning null when it does not exist.
    /// </summary>
    /// <remarks>
    /// Deliberately softer than <see cref="ReadTextAsync"/>: the JSON overloads exist for state that
    /// may legitimately not have been written yet, such as settings on a first run.
    /// </remarks>
    public async Task<T?> ReadJsonAsync<T>(string filePath) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath)) return null;

        string json = await ReadTextAsync(filePath);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }

    public Task WriteJsonAsync<T>(string filePath, T data) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(data);

        return WriteTextAsync(filePath, JsonSerializer.Serialize(data, _jsonOptions));
    }

    public bool FileExists(string filePath) => File.Exists(filePath);

    public bool DirectoryExists(string directoryPath) => Directory.Exists(directoryPath);

    public void CreateDirectory(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        Directory.CreateDirectory(directoryPath);
    }

    public void DeleteFile(string filePath)
    {
        if (File.Exists(filePath)) File.Delete(filePath);
    }

    public void DeleteDirectory(string directoryPath, bool recursive = false)
    {
        if (Directory.Exists(directoryPath)) Directory.Delete(directoryPath, recursive);
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    }
}
