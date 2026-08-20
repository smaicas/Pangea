namespace CdCSharp.Pangea.Supabase;

/// <summary>
/// What the application needs to reach its Supabase project.
/// </summary>
/// <remarks>
/// Configured in <c>App.Configure</c> like every other feature's options. Nothing is guessed from
/// the environment: a client built against the wrong project fails in ways that look like data
/// problems, so the project is named by the application or the feature refuses to start.
/// </remarks>
public class SupabaseOptions
{
    public static SupabaseOptions Default => new();

    /// <summary>The project URL, as <c>https://&lt;ref&gt;.supabase.co</c>.</summary>
    public string Url { get; set; } = "";

    /// <summary>
    /// The project's anonymous key.
    /// </summary>
    /// <remarks>
    /// Public by design - it ships inside every client and identifies the project, not the user -
    /// and useless on its own: what a request may read or write is decided by row level security
    /// against the signed-in user. The <c>service_role</c> key is the opposite of that and must
    /// never be put here, or anywhere else a user's device can read it.
    /// </remarks>
    public string AnonKey { get; set; } = "";

    /// <summary>Open the realtime socket as soon as the client is initialized.</summary>
    public bool AutoConnectRealtime { get; set; } = true;

    /// <summary>Renew the access token before it expires, for as long as the application runs.</summary>
    public bool AutoRefreshToken { get; set; } = true;

    /// <summary>
    /// Keep the signed-in session across restarts.
    /// </summary>
    /// <remarks>
    /// On by default, and the reason an application can sign a user in anonymously at all: the
    /// account only exists as long as its refresh token does, so a session that is not persisted is
    /// a new and empty user every launch.
    /// </remarks>
    public bool PersistSession { get; set; } = true;

    /// <summary>
    /// Where the session is kept, inside the per-platform data directory.
    /// </summary>
    /// <remarks>
    /// It holds a refresh token, which is a credential. On Android and iOS the application sandbox
    /// is what protects it; on a desktop it is a file in the user's profile, readable by anything
    /// running as that user. That is the same guarantee the platform gives every other application,
    /// and it is not encryption - an application handling more than its own data should sign in
    /// against something stronger than an anonymous account.
    /// </remarks>
    public string SessionFileName { get; set; } = "supabase-session.json";

    /// <summary>
    /// The key the session is stored under in the platform's secret store.
    /// </summary>
    /// <remarks>
    /// Where the session actually lives when an <c>ISecretStore</c> is registered, which it is in
    /// every Pangea application. A session found in <see cref="SessionFileName"/> on startup is
    /// moved here and the file is deleted, so an application upgrading does not sign its users out
    /// and does not leave the old credential behind either. Change it to keep two projects'
    /// sessions apart in one application.
    /// </remarks>
    public string SessionSecretKey { get; set; } = "pangea.supabase.session";

    /// <summary>Where the writes made offline are queued, inside the same directory.</summary>
    public string OutboxFileName { get; set; } = "supabase-outbox.json";

    /// <summary>The Postgres schema the client reads and writes.</summary>
    public string Schema { get; set; } = "public";

    /// <summary>
    /// Sign in anonymously during startup when no session was restored.
    /// </summary>
    /// <remarks>
    /// The whole point of an anonymous account: the application is usable before the user has been
    /// asked for anything, and an email can be attached to the same account later without the data
    /// moving. Requires anonymous sign-ins to be enabled on the project.
    /// </remarks>
    public bool SignInAnonymouslyOnStart { get; set; }

    /// <summary>
    /// How long startup waits for the client before giving up on it.
    /// </summary>
    /// <remarks>
    /// A phone that woke up on a captive portal will hold a connection open for as long as it is
    /// allowed to. An application that has anything to show from its own cache should not be behind
    /// a splash for that long, so this is deliberately short and failing it is not fatal - see
    /// <see cref="RequireConnectionAtStartup"/>.
    /// </remarks>
    public TimeSpan InitializeTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Whether a backend that cannot be reached at startup stops the application.
    /// </summary>
    /// <remarks>
    /// Off by default. An application that keeps a local cache has something to show without the
    /// network, and refusing to start is the one behaviour a user on a train cannot work around.
    /// Turn it on only when the first screen genuinely has nothing to draw without the server.
    /// </remarks>
    public bool RequireConnectionAtStartup { get; set; }
}
