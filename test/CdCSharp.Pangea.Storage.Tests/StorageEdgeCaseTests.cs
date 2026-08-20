using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Storage.Providers;
using CdCSharp.Pangea.Storage.Services;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Storage.Tests;

/// <summary>
/// Storage at its edges: names that try to leave the data folder, content that is not plain ASCII,
/// and the failure each operation is supposed to produce.
/// </summary>
public sealed class StorageEdgeCaseTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "pangea-edge-" + Guid.NewGuid().ToString("N"));

    private readonly IStorageService _storage;

    public StorageEdgeCaseTests()
    {
        StorageOptions options = new() { ApplicationName = "EdgeProbe", CustomDataPath = _root };
        _storage = new StorageService(new PortablePlatformPathProvider(Options.Create(options)));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    public sealed class Sample
    {
        public string Name { get; set; } = "";
    }

    [Fact]
    public void ASubfolderIsAPerfectlyGoodDataFileName()
    {
        string path = _storage.GetDataFilePath(Path.Combine("profiles", "default.json"));

        Assert.StartsWith(Path.GetFullPath(_root), path, StringComparison.Ordinal);
        Assert.EndsWith("default.json", path, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>Path.Combine</c> answers an absolute second argument by discarding the first, so this used
    /// to hand back a path outside the data folder without a word.
    /// </summary>
    [Fact]
    public void AnAbsoluteFileName_IsRefused()
    {
        string absolute = Path.Combine(Path.GetTempPath(), "escaped.txt");

        ArgumentException error =
            Assert.Throws<ArgumentException>(() => _storage.GetDataFilePath(absolute));

        Assert.Contains("absolute", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A file name is not a route out of the folder it is meant to be in.</summary>
    [Fact]
    public void AFileNameThatClimbsOutOfTheDataFolder_IsRefused()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => _storage.GetDataFilePath(Path.Combine("..", "..", "escaped.txt")));

        Assert.Contains("outside", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A sibling folder whose name merely starts the same way is still outside.
    /// </summary>
    [Fact]
    public void ASiblingFolderWithASharedPrefix_IsStillOutside()
    {
        string sibling = Path.Combine("..", Path.GetFileName(_root) + ".backup", "f.txt");

        Assert.Throws<ArgumentException>(() => _storage.GetDataFilePath(sibling));
    }

    [Fact]
    public async Task TextSurvivesARoundTripWithAccentsAndEmoji()
    {
        string path = _storage.GetDataFilePath("unicode.txt");
        const string content = "ñandú, 中文, 🎨";

        await _storage.WriteTextAsync(path, content);

        Assert.Equal(content, await _storage.ReadTextAsync(path));
    }

    [Fact]
    public async Task WritingTwice_ReplacesRatherThanAppends()
    {
        string path = _storage.GetDataFilePath("twice.txt");

        await _storage.WriteTextAsync(path, "first");
        await _storage.WriteTextAsync(path, "second");

        Assert.Equal("second", await _storage.ReadTextAsync(path));
    }

    [Fact]
    public async Task JsonSurvivesARoundTripWithAccents()
    {
        string path = _storage.GetDataFilePath("round.json");

        await _storage.WriteJsonAsync(path, new Sample { Name = "Ámbar 中" });

        Assert.Equal("Ámbar 中", (await _storage.ReadJsonAsync<Sample>(path))!.Name);
    }

    /// <summary>
    /// A missing file is an absence and reads as null; a corrupt one is a problem and says so.
    /// Returning null for both would turn damaged data into silently empty data.
    /// </summary>
    [Fact]
    public async Task UnreadableJson_ThrowsRatherThanReadingAsNull()
    {
        string path = _storage.GetDataFilePath("bad.json");
        await _storage.WriteTextAsync(path, "{ this is not json");

        // Its own type, not the IOException family: the file was read perfectly well, and what is
        // wrong with it will be just as wrong on the next attempt.
        StorageSerializationException failure = await Assert.ThrowsAsync<StorageSerializationException>(
            () => _storage.ReadJsonAsync<Sample>(path));

        Assert.Equal(path, failure.FilePath);
        Assert.Equal(typeof(Sample), failure.DataType);
        Assert.IsAssignableFrom<System.Text.Json.JsonException>(failure.InnerException);

        Assert.Null(await _storage.ReadJsonAsync<Sample>(_storage.GetDataFilePath("absent.json")));
    }

    [Fact]
    public async Task WritingNullAsJson_IsRefused()
    {
        string path = _storage.GetDataFilePath("null.json");

        await Assert.ThrowsAsync<ArgumentNullException>(() => _storage.WriteJsonAsync<Sample>(path, null!));
    }

    /// <summary>
    /// The default is the non-recursive one, so deleting a folder with anything in it fails rather
    /// than taking the contents with it.
    /// </summary>
    [Fact]
    public async Task DeletingANonEmptyDirectory_NeedsToBeAskedForExplicitly()
    {
        string directory = Path.Combine(Path.GetFullPath(_root), "full");
        _storage.CreateDirectory(directory);
        await _storage.WriteTextAsync(Path.Combine(directory, "a.txt"), "x");

        Assert.Throws<IOException>(() => _storage.DeleteDirectory(directory));
        Assert.True(_storage.DirectoryExists(directory));

        _storage.DeleteDirectory(directory, recursive: true);

        Assert.False(_storage.DirectoryExists(directory));
    }

    /// <summary>
    /// The two absences are distinguishable, and worth keeping so: a file that is not there yet is
    /// an ordinary state, a folder that is not there usually means nothing was ever written.
    /// </summary>
    [Fact]
    public async Task AMissingFileAndAMissingFolder_FailDifferently()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => _storage.ReadTextAsync(_storage.GetDataFilePath("missing.txt")));

        _storage.CreateDirectory(Path.GetFullPath(_root));

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _storage.ReadTextAsync(_storage.GetDataFilePath("missing.txt")));
    }
}
