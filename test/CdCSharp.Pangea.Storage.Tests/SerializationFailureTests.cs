using CdCSharp.Pangea.Storage;
using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Storage.Providers;
using CdCSharp.Pangea.Storage.Services;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Storage.Tests;

/// <summary>
/// "I cannot write this file" and "I cannot serialize this object" are different problems with
/// different answers, and an application that catches them together silently loses everything it
/// meant to save.
/// </summary>
public class SerializationFailureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pangea-serialization", Guid.NewGuid().ToString("N"));
    private readonly StorageService _storage;

    public SerializationFailureTests()
    {
        Directory.CreateDirectory(_root);

        _storage = new StorageService(new PortablePlatformPathProvider(
            Options.Create(new StorageOptions { UsePortableMode = true, CustomDataPath = _root })));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>A type that cannot be written, because writing it never ends.</summary>
    private sealed class Loop
    {
        public Loop? Self { get; set; }
    }

    [Fact]
    public async Task SomethingThatCannotBeSerialized_SaysSoInItsOwnType()
    {
        Loop loop = new();
        loop.Self = loop;

        string path = _storage.GetDataFilePath("loop.json");

        StorageSerializationException failure = await Assert.ThrowsAsync<StorageSerializationException>(
            () => _storage.WriteJsonAsync(path, loop));

        Assert.Equal(path, failure.FilePath);
        Assert.Equal(typeof(Loop), failure.DataType);
        Assert.NotNull(failure.InnerException);

        // Not an IO failure: retrying this later will fail in exactly the same way.
        Assert.IsNotAssignableFrom<IOException>(failure);
    }

    /// <summary>
    /// The file is written after the object has been serialized, so a failure leaves whatever was
    /// there before rather than an empty file where the settings used to be.
    /// </summary>
    [Fact]
    public async Task AFailedWrite_LeavesTheOldContentsAlone()
    {
        string path = _storage.GetDataFilePath("settings.json");

        await _storage.WriteTextAsync(path, "{\"kept\":true}");

        Loop loop = new();
        loop.Self = loop;

        await Assert.ThrowsAsync<StorageSerializationException>(() => _storage.WriteJsonAsync(path, loop));

        Assert.Equal("{\"kept\":true}", await _storage.ReadTextAsync(path));
    }
}
