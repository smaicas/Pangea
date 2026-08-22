namespace PangeaSupabaseApp.Data;

/// <summary>
/// The server, as everything above it needs to see it.
/// </summary>
/// <remarks>
/// <para>
/// The repository depends on this rather than on <see cref="NotesBackend"/> so that "the network is
/// gone" and "the server said no" can be arranged in a test. Those two cases are the whole point of
/// the outbox - one comes back and the other never will - and there is no way to provoke either
/// against a real Supabase project on demand.
/// </para>
/// <para>
/// A seam worth having exactly here and nowhere else: this is the boundary where the application
/// stops being able to decide what happens.
/// </para>
/// </remarks>
public interface INotesBackend
{
    /// <summary>Signs in - anonymously, the first time - and answers who that is.</summary>
    Task<string> SignInAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Note>> AllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Note note, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
