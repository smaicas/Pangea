using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Storage.Services;

namespace CdCSharp.Pangea.Storage.Tests;

/// <summary>
/// Writes create the folders they need; reads do not touch the filesystem beyond reading.
/// </summary>
public class StorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pangea-storage-" + Guid.NewGuid().ToString("N"));
    private readonly StorageService _storage;

    public StorageServiceTests() => _storage = new StorageService(new TempPathProvider(_root));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    public sealed record Settings(string Theme, int Size);

    [Fact]
    public async Task ReadingAMissingFile_DoesNotCreateItsDirectory()
    {
        string filePath = Path.Combine(_root, "nested", "missing.txt");

        // FileNotFound when the folder exists, DirectoryNotFound when it does not: both are IOException.
        await Assert.ThrowsAnyAsync<IOException>(() => _storage.ReadTextAsync(filePath));

        Assert.False(Directory.Exists(Path.Combine(_root, "nested")));
    }

    [Fact]
    public async Task WritingCreatesTheDirectoryItNeeds()
    {
        string filePath = Path.Combine(_root, "nested", "deeper", "notes.txt");

        await _storage.WriteTextAsync(filePath, "content");

        Assert.True(File.Exists(filePath));
        Assert.Equal("content", await _storage.ReadTextAsync(filePath));
    }

    [Fact]
    public async Task ReadJson_ReturnsNullForAMissingFile()
    {
        // Softer than ReadTextAsync on purpose: settings may legitimately not exist yet.
        string filePath = Path.Combine(_root, "settings.json");

        Assert.Null(await _storage.ReadJsonAsync<Settings>(filePath));
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task JsonRoundTripsThroughTheFile()
    {
        string filePath = _storage.GetDataFilePath("settings.json");

        await _storage.WriteJsonAsync(filePath, new Settings("Dark", 14));
        Settings? loaded = await _storage.ReadJsonAsync<Settings>(filePath);

        Assert.Equal(new Settings("Dark", 14), loaded);
    }

    [Fact]
    public void GetDataFilePath_SitsUnderTheApplicationDataFolder()
    {
        Assert.Equal(Path.Combine(_root, "app", "file.txt"), _storage.GetDataFilePath("file.txt"));
    }

    [Fact]
    public void DeletingWhatIsNotThere_IsANoOp()
    {
        _storage.DeleteFile(Path.Combine(_root, "ghost.txt"));
        _storage.DeleteDirectory(Path.Combine(_root, "ghost"));
    }

    [Fact]
    public void CreateDirectory_IsIdempotent()
    {
        string directory = Path.Combine(_root, "twice");

        _storage.CreateDirectory(directory);
        _storage.CreateDirectory(directory);

        Assert.True(_storage.DirectoryExists(directory));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankPaths_AreRejected(string path)
    {
        Assert.Throws<ArgumentException>(() => _storage.GetDataFilePath(path));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.ReadTextAsync(path));
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.WriteTextAsync(path, "x"));
    }

    [Fact]
    public void MissingPathProvider_IsRejected() =>
        Assert.Throws<ArgumentNullException>(() => new StorageService(null!));

    private sealed class TempPathProvider(string root) : IPlatformPathProvider
    {
        public string GetApplicationDataPath() => Path.Combine(root, "app");

        public string GetUserDataPath() => Path.Combine(root, "user");

        public string GetTempPath() => Path.Combine(root, "temp");

        public string GetCachePath() => Path.Combine(root, "cache");
    }
}
