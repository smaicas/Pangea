using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Supabase.Abstractions;
using CdCSharp.Pangea.Supabase.Services;
using CdCSharp.Pangea.Testing.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Supabase.Tests;

/// <summary>
/// What the outbox does when the file underneath it will not cooperate.
/// </summary>
/// <remarks>
/// The two failures look the same from a distance and are opposites. A file that is not an outbox
/// holds nothing worth keeping. A file that could not be opened this time holds everything the user
/// did offline, and answering the next write by saving one entry over it is how all of it is lost -
/// silently, on the one connection that needed a queue in the first place.
/// </remarks>
public class OutboxFailureTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Storage that behaves until it is asked to read, and then fails the way a disk does.</summary>
    private sealed class UnreadableStorage(IStorageService inner, Exception failure) : IStorageService
    {
        public string GetApplicationDataPath() => inner.GetApplicationDataPath();

        public string GetUserDataPath() => inner.GetUserDataPath();

        public string GetTempPath() => inner.GetTempPath();

        public string GetCachePath() => inner.GetCachePath();

        public string GetDataFilePath(string fileName) => inner.GetDataFilePath(fileName);

        public Task<string> ReadTextAsync(string filePath) => throw failure;

        public Task WriteTextAsync(string filePath, string content) => inner.WriteTextAsync(filePath, content);

        public Task<T?> ReadJsonAsync<T>(string filePath) where T : class => throw failure;

        public Task WriteJsonAsync<T>(string filePath, T data) where T : class => inner.WriteJsonAsync(filePath, data);

        public bool FileExists(string filePath) => inner.FileExists(filePath);

        public bool DirectoryExists(string directoryPath) => inner.DirectoryExists(directoryPath);

        public void CreateDirectory(string directoryPath) => inner.CreateDirectory(directoryPath);

        public void DeleteFile(string filePath) => inner.DeleteFile(filePath);

        public void DeleteDirectory(string directoryPath, bool recursive = false) =>
            inner.DeleteDirectory(directoryPath, recursive);
    }

    private static FileOutbox Build(IStorageService storage) =>
        new(storage, Options.Create(new SupabaseOptions()), NullLogger<FileOutbox>.Instance);

    /// <summary>
    /// A file that is not an outbox is worth nothing, so the queue starts again rather than making
    /// the application unusable.
    /// </summary>
    [Fact]
    public async Task ContentsThatAreNotAnOutbox_AreTreatedAsAnEmptyQueue()
    {
        InMemoryStorageService files = new();
        FileOutbox outbox = Build(new UnreadableStorage(
            files, new StorageSerializationException("not an outbox")));

        await files.WriteTextAsync(files.GetDataFilePath(new SupabaseOptions().OutboxFileName), "{ nonsense");

        Assert.Empty(await outbox.PendingAsync(Ct));

        // And a write afterwards still works: the corrupt file is replaced by a real queue.
        await outbox.EnqueueAsync("insert", "{}", Ct);
    }

    /// <summary>
    /// A file that could not be opened this time still holds the queue. Enqueueing has to fail
    /// rather than quietly replace it with the one entry being written.
    /// </summary>
    [Fact]
    public async Task AFileThatCouldNotBeOpened_FailsTheWriteInsteadOfEmptyingTheQueue()
    {
        InMemoryStorageService files = new();
        string path = files.GetDataFilePath(new SupabaseOptions().OutboxFileName);

        await files.WriteTextAsync(path, "[]");

        FileOutbox outbox = Build(new UnreadableStorage(files, new IOException("the file is in use")));

        await Assert.ThrowsAsync<IOException>(() => outbox.EnqueueAsync("insert", "{}", Ct));
        await Assert.ThrowsAsync<IOException>(() => outbox.PendingAsync(Ct));

        // Nothing was written over it: what the file held is what it still holds.
        Assert.Equal("[]", await files.ReadTextAsync(path));
    }
}
