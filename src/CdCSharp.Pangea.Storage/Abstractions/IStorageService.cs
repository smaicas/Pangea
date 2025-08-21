namespace CdCSharp.Pangea.Storage.Abstractions;
public interface IStorageService
{
    string GetApplicationDataPath();
    string GetUserDataPath();
    string GetTempPath();
    string GetCachePath();
    string GetDataFilePath(string fileName);
    
    Task<string> ReadTextAsync(string filePath);
    Task WriteTextAsync(string filePath, string content);
    Task<T?> ReadJsonAsync<T>(string filePath) where T : class;
    Task WriteJsonAsync<T>(string filePath, T data) where T : class;
    
    bool FileExists(string filePath);
    bool DirectoryExists(string directoryPath);
    void CreateDirectory(string directoryPath);
    void DeleteFile(string filePath);
    void DeleteDirectory(string directoryPath, bool recursive = false);
}

public interface IPlatformPathProvider
{
    string GetApplicationDataPath();
    string GetUserDataPath();
    string GetTempPath();
    string GetCachePath();
}