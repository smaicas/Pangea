using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Supabase.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Supabase.Services;

/// <inheritdoc cref="IOutbox"/>
/// <remarks>
/// A JSON file in the per-platform data directory, rewritten whole on every change. That is the
/// right trade for a queue that holds the writes one person made while their train was in a tunnel:
/// it is a handful of entries, and a format anyone can read while working out why one of them never
/// landed.
/// </remarks>
internal sealed class FileOutbox : IOutbox
{
    private readonly IStorageService _storage;
    private readonly ILogger<FileOutbox> _logger;
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileOutbox(IStorageService storage, IOptions<SupabaseOptions> options, ILogger<FileOutbox> logger)
    {
        _storage = storage;
        _logger = logger;
        _path = storage.GetDataFilePath(options.Value.OutboxFileName);
    }

    public async Task<IReadOnlyList<OutboxEntry>> PendingAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await ReadAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OutboxEntry> EnqueueAsync(
        string kind, string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(payload);

        OutboxEntry entry = new(Guid.NewGuid().ToString("N"), kind, payload, DateTimeOffset.UtcNow, Attempts: 0);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            List<OutboxEntry> queued = [.. await ReadAsync().ConfigureAwait(false), entry];

            await WriteAsync(queued).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        return entry;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The handler runs outside the lock. It performs a network request, and holding the queue for
    /// the length of one would block every write the user makes while it is in flight - on a
    /// connection slow enough to need an outbox in the first place.
    /// </remarks>
    public async Task<int> DrainAsync(
        Func<OutboxEntry, CancellationToken, Task<bool>> handler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        int accepted = 0;

        foreach (OutboxEntry entry in await PendingAsync(cancellationToken).ConfigureAwait(false))
        {
            bool done;

            try
            {
                done = await handler(entry, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Replaying queued write {Kind} ({Id}) failed", entry.Kind, entry.Id);
                done = false;
            }

            if (!done)
            {
                await RecordAttemptAsync(entry.Id, cancellationToken).ConfigureAwait(false);
                break;
            }

            await RemoveAsync(entry.Id, cancellationToken).ConfigureAwait(false);
            accepted++;
        }

        return accepted;
    }

    public async Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await MutateAsync(queued => queued.RemoveAll(entry => entry.Id == id) > 0, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await MutateAsync(queued =>
        {
            bool any = queued.Count > 0;
            queued.Clear();
            return any;
        }, cancellationToken).ConfigureAwait(false);
    }

    private Task RecordAttemptAsync(string id, CancellationToken cancellationToken) =>
        MutateAsync(queued =>
        {
            int index = queued.FindIndex(entry => entry.Id == id);

            if (index < 0) return false;

            queued[index] = queued[index] with { Attempts = queued[index].Attempts + 1 };
            return true;
        }, cancellationToken);

    private async Task MutateAsync(Func<List<OutboxEntry>, bool> change, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            List<OutboxEntry> queued = [.. await ReadAsync().ConfigureAwait(false)];

            if (change(queued)) await WriteAsync(queued).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// What is queued, and nothing when the file is missing or unreadable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A queue whose contents are not an outbox is reported and then treated as empty. The
    /// alternative - throwing from every write the application makes afterwards - turns one corrupt
    /// file into an application that cannot be used at all, and the entries are gone either way.
    /// </para>
    /// <para>
    /// A file that could not be read <em>this time</em> is a different thing entirely and is left to
    /// propagate. Treating it as empty would answer the next write by saving one entry over a queue
    /// that was merely locked, which is how a transient failure to open a file turns into losing
    /// everything the user did offline.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyList<OutboxEntry>> ReadAsync()
    {
        if (!_storage.FileExists(_path)) return [];

        try
        {
            return await _storage.ReadJsonAsync<List<OutboxEntry>>(_path).ConfigureAwait(false) ?? [];
        }
        catch (StorageSerializationException ex)
        {
            // The file is there and is not an outbox. Nothing is going to make it one, and the
            // entries are gone whatever happens next.
            _logger.LogError(ex, "The outbox at {Path} could not be read; the writes it held are lost", _path);
            return [];
        }
    }

    private Task WriteAsync(List<OutboxEntry> queued) => _storage.WriteJsonAsync(_path, queued);
}
