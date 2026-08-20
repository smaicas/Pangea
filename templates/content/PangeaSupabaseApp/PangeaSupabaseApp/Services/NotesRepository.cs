using CdCSharp.Pangea.Supabase.Abstractions;
using Microsoft.Extensions.Logging;
using PangeaSupabaseApp.Data;
using System.Text.Json;

namespace PangeaSupabaseApp.Services;

/// <summary>
/// What the screens talk to, made to work on a phone in a tunnel.
/// </summary>
/// <remarks>
/// <para>
/// Every read answers from the cache first and refreshes behind it. Every write is applied to the
/// cache immediately, sent, and - when sending fails - queued to be replayed. The user is never
/// waiting on a request and never loses a write.
/// </para>
/// <para>
/// <see cref="Changed"/> is raised on whatever thread the work finished on. A view model has to
/// marshal through <c>IUIDispatcher</c> before touching bound state.
/// </para>
/// </remarks>
public sealed class NotesRepository
{
    private static readonly JsonSerializerOptions Payloads = new(JsonSerializerDefaults.Web);

    private readonly NotesBackend _backend;
    private readonly NotesCache _cache;
    private readonly IOutbox _outbox;
    private readonly ILogger<NotesRepository> _logger;

    public NotesRepository(NotesBackend backend, NotesCache cache, IOutbox outbox, ILogger<NotesRepository> logger)
    {
        _backend = backend;
        _cache = cache;
        _outbox = outbox;
        _logger = logger;
    }

    /// <summary>Raised when the notes have changed, from a local write or from the server.</summary>
    public event EventHandler<IReadOnlyList<Note>>? Changed;

    public Task<string> SignInAsync(CancellationToken cancellationToken = default) =>
        _backend.SignInAsync(cancellationToken);

    /// <summary>How many writes are waiting for the network.</summary>
    public async Task<int> PendingAsync(CancellationToken cancellationToken = default) =>
        (await _outbox.PendingAsync(cancellationToken).ConfigureAwait(false)).Count;

    /// <summary>What the device remembers. The refresh follows and raises <see cref="Changed"/>.</summary>
    public async Task<IReadOnlyList<Note>> AllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Note> cached = await _cache.ReadAsync().ConfigureAwait(false);

        _ = RefreshAsync(cancellationToken);

        return cached;
    }

    public async Task<IReadOnlyList<Note>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<Note> fresh = await _backend.AllAsync(cancellationToken).ConfigureAwait(false);

            await _cache.WriteAsync(fresh).ConfigureAwait(false);

            Changed?.Invoke(this, fresh);

            return fresh;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The notes could not be refreshed; the cached list stands");

            return await _cache.ReadAsync().ConfigureAwait(false);
        }
    }

    public async Task AddAsync(string title, CancellationToken cancellationToken = default)
    {
        // The id is assigned here so the note keeps its identity whether or not the request lands.
        Note note = new(Guid.NewGuid(), title.Trim(), DateTimeOffset.UtcNow);

        await ApplyLocally(notes => [note, .. notes]).ConfigureAwait(false);

        await Send(() => _backend.AddAsync(note, cancellationToken), "note.add", note, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await ApplyLocally(notes => [.. notes.Where(note => note.Id != id)]).ConfigureAwait(false);

        await Send(() => _backend.DeleteAsync(id, cancellationToken), "note.delete", id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Replays everything queued while the network was gone.</summary>
    /// <returns>How many writes landed.</returns>
    public async Task<int> SyncAsync(CancellationToken cancellationToken = default)
    {
        int sent = await _outbox.DrainAsync(Replay, cancellationToken).ConfigureAwait(false);

        if (sent > 0) await RefreshAsync(cancellationToken).ConfigureAwait(false);

        return sent;
    }

    /// <summary>
    /// Sends a write, and keeps it when the send fails.
    /// </summary>
    /// <remarks>
    /// Only a failure to reach the server is queued. A request the server refused would be refused
    /// every time, and retrying it forever is how an outbox becomes a stuck queue that blocks every
    /// write behind it.
    /// </remarks>
    private async Task Send<T>(Func<Task> send, string kind, T payload, CancellationToken cancellationToken)
    {
        try
        {
            await send().ConfigureAwait(false);
        }
        catch (Exception ex) when (Unreachable(ex))
        {
            _logger.LogInformation("{Kind} was queued: the backend is not reachable", kind);

            await _outbox.EnqueueAsync(kind, JsonSerializer.Serialize(payload, Payloads), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<bool> Replay(OutboxEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            switch (entry.Kind)
            {
                case "note.add":
                    await _backend.AddAsync(Read<Note>(entry), cancellationToken).ConfigureAwait(false);
                    return true;

                case "note.delete":
                    await _backend.DeleteAsync(Read<Guid>(entry), cancellationToken).ConfigureAwait(false);
                    return true;

                default:
                    // Written by a build that knew a kind this one does not. Dropped rather than
                    // retried forever, which would block every write queued behind it.
                    _logger.LogWarning("Dropping queued write of unknown kind {Kind}", entry.Kind);
                    return true;
            }
        }
        catch (Exception ex) when (Unreachable(ex))
        {
            // Still out of reach. Said plainly rather than left to escape: the outbox does treat a
            // handler that throws as a refusal, but relying on being caught elsewhere is one
            // refactor away from taking a sync down with it.
            _logger.LogInformation("Queued write {Kind} is still waiting for the network", entry.Kind);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The server had an opinion, and it will have the same one next time.
            _logger.LogError(ex, "Queued write {Kind} was refused and has been dropped", entry.Kind);
            return true;
        }
    }

    private static T Read<T>(OutboxEntry entry) =>
        JsonSerializer.Deserialize<T>(entry.Payload, Payloads)
        ?? throw new InvalidOperationException($"Queued write {entry.Id} could not be read back.");

    /// <summary>
    /// Whether this is the network being absent rather than the server saying no.
    /// </summary>
    /// <remarks>
    /// The distinction is what keeps the queue moving: one comes back and the other never will.
    /// Nothing more specific than the transport is inspected, because a refusal that arrived at all
    /// is a refusal.
    /// </remarks>
    private static bool Unreachable(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or TimeoutException ||
        exception.InnerException is HttpRequestException or TimeoutException;

    private async Task ApplyLocally(Func<IReadOnlyList<Note>, IReadOnlyList<Note>> change)
    {
        IReadOnlyList<Note> updated = change(await _cache.ReadAsync().ConfigureAwait(false));

        await _cache.WriteAsync(updated).ConfigureAwait(false);

        Changed?.Invoke(this, updated);
    }
}
