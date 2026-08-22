using CdCSharp.Pangea.Supabase.Abstractions;
using Supabase.Postgrest;
using Client = Supabase.Client;

namespace PangeaSupabaseApp.Data;

/// <summary>
/// The only class that knows Supabase exists.
/// </summary>
/// <remarks>
/// Everything above it works in <see cref="Note"/>, so a schema change stops here. Every method may
/// fail because the network is not there - which is not exceptional on a phone, and is why the
/// repository above catches it rather than this reporting it.
/// </remarks>
public sealed class NotesBackend : INotesBackend
{
    private readonly ISupabaseClientProvider _backend;
    private readonly ISupabaseAuth _auth;

    public NotesBackend(ISupabaseClientProvider backend, ISupabaseAuth auth)
    {
        _backend = backend;
        _auth = auth;
    }

    /// <summary>Signs in - anonymously, the first time - and answers who that is.</summary>
    public async Task<string> SignInAsync(CancellationToken cancellationToken = default)
    {
        await _auth.EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);

        return _auth.UserId ?? throw new InvalidOperationException("Signed in with no user id.");
    }

    public async Task<IReadOnlyList<Note>> AllAsync(CancellationToken cancellationToken = default)
    {
        Client client = await _backend.InitializeAsync(cancellationToken).ConfigureAwait(false);

        // No filter on the owner: the read policy is the filter. Restating it here would be one
        // more place to get wrong, and the server would ignore it anyway.
        List<NoteRow> rows = (await client.From<NoteRow>()
            .Order("created_at", Constants.Ordering.Descending)
            .Get(cancellationToken).ConfigureAwait(false)).Models;

        return [.. rows.Select(row => new Note(row.Id, row.Title, new DateTimeOffset(row.CreatedAt, TimeSpan.Zero)))];
    }

    public async Task AddAsync(Note note, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(note);

        string owner = _auth.UserId ?? throw new InvalidOperationException("Nobody is signed in.");

        Client client = await _backend.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await client.From<NoteRow>().Insert(
            new NoteRow
            {
                Id = note.Id,
                OwnerId = owner,
                Title = note.Title,
                CreatedAt = note.CreatedAt.UtcDateTime
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Client client = await _backend.InitializeAsync(cancellationToken).ConfigureAwait(false);

        await client.From<NoteRow>().Where(row => row.Id == id)
            .Delete(cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
