using CdCSharp.Pangea.Storage.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CdCSharp.Pangea.Storage.Services;

public class StorageService : IStorageService
{
    private readonly IPlatformPathProvider _pathProvider;
    private readonly JsonSerializerOptions _jsonOptions;

    public StorageService(IPlatformPathProvider pathProvider)
    {
        _pathProvider = pathProvider;
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

    public string GetDataFilePath(string fileName) => Path.Combine(GetApplicationDataPath(), fileName);

    public async Task<string> ReadTextAsync(string filePath)
    {
        EnsureDirectoryExists(filePath);
        return await File.ReadAllTextAsync(filePath);
    }

    public async Task WriteTextAsync(string filePath, string content)
    {
        EnsureDirectoryExists(filePath);
        await File.WriteAllTextAsync(filePath, content);
    }

    public async Task<T?> ReadJsonAsync<T>(string filePath) where T : class
    {
        if (!File.Exists(filePath)) return null;
        
        string json = await ReadTextAsync(filePath);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }

    public async Task WriteJsonAsync<T>(string filePath, T data) where T : class
    {
        string json = JsonSerializer.Serialize(data, _jsonOptions);
        await WriteTextAsync(filePath, json);
    }

    public bool FileExists(string filePath) => File.Exists(filePath);
    public bool DirectoryExists(string directoryPath) => Directory.Exists(directoryPath);
    
    public void CreateDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);
    }

    public void DeleteFile(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    public void DeleteDirectory(string directoryPath, bool recursive = false)
    {
        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive);
    }

    private void EnsureDirectoryExists(string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            CreateDirectory(directory);
    }
}