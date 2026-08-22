using CdCSharp.Pangea.Supabase.Abstractions;
using PangeaSupabaseApp.Data;

namespace PangeaSupabaseApp.Tests;

/// <summary>
/// A backend that can be told to be unreachable, or to refuse.
/// </summary>
/// <remarks>
/// The distinction is the whole point of the tests that use it. An absent network comes back and
/// the write is worth keeping; a refusal never does, and retrying it forever is how an outbox
/// becomes a queue that blocks everything behind it.
/// </remarks>
public sealed class FakeBackend : INotesBackend
{
    private readonly List<Note> _notes = [];

    /// <summary>What the next call throws, or null to let it through.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Stops the network, the way a tunnel does.</summary>
    public void GoOffline() => Failure = new HttpRequestException("No such host is known.");

    /// <summary>Refuses every call, the way a rejected row does.</summary>
    public void Refuse() => Failure = new InvalidOperationException("new row violates row-level security policy");

    public void ComeBack() => Failure = null;

    /// <summary>What actually landed on the server.</summary>
    public IReadOnlyList<Note> Stored => _notes;

    public Task<string> SignInAsync(CancellationToken cancellationToken = default) =>
        Failure is not null ? Task.FromException<string>(Failure) : Task.FromResult("user-1");

    public Task<IReadOnlyList<Note>> AllAsync(CancellationToken cancellationToken = default) =>
        Failure is not null
            ? Task.FromException<IReadOnlyList<Note>>(Failure)
            : Task.FromResult<IReadOnlyList<Note>>([.. _notes]);

    public Task AddAsync(Note note, CancellationToken cancellationToken = default)
    {
        if (Failure is not null) return Task.FromException(Failure);

        _notes.Insert(0, note);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (Failure is not null) return Task.FromException(Failure);

        _notes.RemoveAll(note => note.Id == id);

        return Task.CompletedTask;
    }
}

/// <summary>
/// The queue, in memory.
/// </summary>
/// <remarks>
/// The shipped outbox writes to the device, which a test has no use for. Written out here rather
/// than mocked because the contract is short and its two rules - oldest first, and a drain stops at
/// the first entry the handler will not accept - are exactly what the tests are checking the
/// repository against.
/// </remarks>
public sealed class InMemoryOutbox : IOutbox
{
    private readonly List<OutboxEntry> _entries = [];

    public Task<IReadOnlyList<OutboxEntry>> PendingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OutboxEntry>>([.. _entries]);

    public Task<OutboxEntry> EnqueueAsync(string kind, string payload, CancellationToken cancellationToken = default)
    {
        OutboxEntry entry = new(Guid.NewGuid().ToString("N"), kind, payload, DateTimeOffset.UtcNow, 0);

        _entries.Add(entry);

        return Task.FromResult(entry);
    }

    public async Task<int> DrainAsync(
        Func<OutboxEntry, CancellationToken, Task<bool>> handler, CancellationToken cancellationToken = default)
    {
        int accepted = 0;

        foreach (OutboxEntry entry in _entries.ToList())
        {
            if (!await handler(entry, cancellationToken)) break;

            _entries.Remove(entry);
            accepted++;
        }

        return accepted;
    }

    public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        _entries.RemoveAll(entry => entry.Id == id);

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _entries.Clear();

        return Task.CompletedTask;
    }
}
