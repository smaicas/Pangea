using Supabase;

namespace CdCSharp.Pangea.Supabase.Abstractions;

/// <summary>
/// The application's one Supabase client, and whether it has reached the project yet.
/// </summary>
/// <remarks>
/// <para>
/// Injected rather than the <see cref="Client"/> itself, for the reason a view model asks for
/// <c>IPangeaDbContext</c> and never a <c>DbContext</c>: a client that has not been initialized
/// looks identical to one that has, right up until the first request fails in a way that reads as
/// a backend fault. Asking through here is what makes "not connected yet" a state the caller can
/// see instead of a bug it has to recognise.
/// </para>
/// <para>
/// Everything on it is safe to call from any thread; nothing on it touches the UI.
/// </para>
/// </remarks>
public interface ISupabaseClientProvider
{
    /// <summary>
    /// The client.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The client has not been initialized. Startup does that; a caller reaching this before it
    /// finished is asking too early.
    /// </exception>
    Client Client { get; }

    /// <summary>Whether <see cref="Client"/> can be used.</summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Builds and initializes the client, or returns the one already built.
    /// </summary>
    /// <remarks>
    /// Called by the startup initializer, and safe to call again: concurrent callers wait on the
    /// same attempt rather than starting a second one. A failed attempt is not cached - the network
    /// it failed on is usually back a moment later.
    /// </remarks>
    Task<Client> InitializeAsync(CancellationToken cancellationToken = default);
}
