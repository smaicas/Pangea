using CdCSharp.Pangea.Supabase.Abstractions;
using Microsoft.Extensions.Logging;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using static Supabase.Gotrue.Constants;

namespace CdCSharp.Pangea.Supabase.Services;

/// <inheritdoc cref="ISupabaseAuth"/>
internal sealed class SupabaseAuth : ISupabaseAuth
{
    private readonly ISupabaseClientProvider _clients;
    private readonly ILogger<SupabaseAuth> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IGotrueClient<User, Session>? _listening;
    private string? _pendingVerifier;

    public SupabaseAuth(ISupabaseClientProvider clients, ILogger<SupabaseAuth> logger)
    {
        _clients = clients;
        _logger = logger;
    }

    public event EventHandler<SupabaseAuthChange>? Changed;

    public User? CurrentUser => _clients.IsInitialized ? _clients.Client.Auth.CurrentUser : null;

    public string? UserId => CurrentUser?.Id;

    public bool IsSignedIn => CurrentUser is not null;

    public bool IsAnonymous => CurrentUser?.IsAnonymous ?? false;

    public async Task<User> EnsureSignedInAsync(CancellationToken cancellationToken = default)
    {
        IGotrueClient<User, Session> auth = await AuthAsync(cancellationToken).ConfigureAwait(false);

        // The restored session is the common case, and signing in over it would abandon the account
        // it belongs to - which for an anonymous user means abandoning everything they have. Logged
        // either way: an account that is different after a restart is the hardest failure in this
        // whole area to see, and every symptom of it points somewhere else.
        if (auth.CurrentUser is { } signedIn)
        {
            _logger.LogInformation("Restored the stored session as {UserId}", signedIn.Id);

            return signedIn;
        }

        _logger.LogWarning("No session was restored; signing in as a new anonymous account");

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (auth.CurrentUser is { } arrived) return arrived;

            Session session = await auth.SignInAnonymously().ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    "Signing in anonymously returned no session. Anonymous sign-ins have to be enabled on the " +
                    "Supabase project before an application can use them.");

            _logger.LogInformation("Signed in anonymously as {UserId}", session.User?.Id);

            return session.User ?? throw new InvalidOperationException(
                "Signing in anonymously returned a session with no user.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<UserIdentity> Identities => CurrentUser?.Identities ?? [];

    public async Task<Uri> StartLinkAsync(
        Provider provider, string redirectTo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectTo);

        IGotrueClient<User, Session> auth = await AuthAsync(cancellationToken).ConfigureAwait(false);

        if (auth.CurrentUser is null)
        {
            throw new InvalidOperationException(
                "There is no signed-in account to attach a provider to. Call EnsureSignedInAsync first.");
        }

        ProviderAuthState state = await auth.LinkIdentity(
            provider,
            new SignInOptions { RedirectTo = redirectTo, FlowType = OAuthFlowType.PKCE }).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Linking {provider} returned no address to open.");

        // In memory and deliberately nowhere else. The verifier is the half of the exchange that
        // does not travel through the browser, and writing it down would turn an intercepted
        // redirect into a session. It is needed for the seconds between here and the callback.
        Interlocked.Exchange(ref _pendingVerifier, state.PKCEVerifier);

        return state.Uri;
    }

    public async Task<User> CompleteLinkAsync(Uri callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        // Read once: a callback that arrives twice - and on Android a redirect can - must not be
        // exchanged twice, and the second exchange fails in a way that reads like a broken link.
        string verifier = Interlocked.Exchange(ref _pendingVerifier, null)
            ?? throw new InvalidOperationException(
                "No link is waiting to be completed. One was never started, it was already completed, or the " +
                "application was restarted while the browser was open.");

        if ((Parameter(callback, "error_description") ?? Parameter(callback, "error")) is { } refusal)
        {
            throw new InvalidOperationException($"The provider refused the link: {refusal}");
        }

        string code = Parameter(callback, "code")
            ?? throw new InvalidOperationException($"The callback {callback.Scheme}://… carried no authorization code.");

        IGotrueClient<User, Session> auth = await AuthAsync(cancellationToken).ConfigureAwait(false);

        Session session = await auth.ExchangeCodeForSession(verifier, code).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Exchanging the authorization code returned no session.");

        _logger.LogInformation("A provider was linked to {UserId}", session.User?.Id);

        return session.User ?? throw new InvalidOperationException(
            "Exchanging the authorization code returned a session with no user.");
    }

    public async Task<User> LinkWithIdTokenAsync(
        Provider provider, string idToken, string? nonce = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idToken);

        IGotrueClient<User, Session> auth = await AuthAsync(cancellationToken).ConfigureAwait(false);

        Session session = await auth.LinkIdentityWithIdToken(
            new LinkIdentityWithIdTokenOptions(provider, idToken, nonce: nonce)).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Linking {provider} from an id token returned no session.");

        _logger.LogInformation("{Provider} was linked to {UserId} from an id token", provider, session.User?.Id);

        return session.User ?? throw new InvalidOperationException(
            $"Linking {provider} from an id token returned a session with no user.");
    }

    public async Task UnlinkAsync(Provider provider, CancellationToken cancellationToken = default)
    {
        IGotrueClient<User, Session> auth = await AuthAsync(cancellationToken).ConfigureAwait(false);

        UserIdentity identity =
            Identities.FirstOrDefault(candidate =>
                string.Equals(candidate.Provider, provider.ToString(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"{provider} is not attached to this account.");

        await auth.UnlinkIdentity(identity).ConfigureAwait(false);
    }

    /// <summary>
    /// One value out of a callback's query, or out of its fragment when the flow put it there.
    /// </summary>
    /// <remarks>
    /// Hand-parsed rather than through a query helper: the callback is a custom scheme, and the
    /// helpers that ship in the framework are shaped for http URLs and for a web request that
    /// this is not.
    /// </remarks>
    private static string? Parameter(Uri callback, string name)
    {
        string query = callback.Query.TrimStart('?');

        if (query.Length == 0) query = callback.Fragment.TrimStart('#');

        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int split = pair.IndexOf('=');

            if (split <= 0) continue;
            if (!pair.AsSpan(0, split).Equals(name, StringComparison.Ordinal)) continue;

            return Uri.UnescapeDataString(pair[(split + 1)..].Replace('+', ' '));
        }

        return null;
    }

    public async Task LinkEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        IGotrueClient<User, Session> auth = await AuthAsync(cancellationToken).ConfigureAwait(false);

        if (auth.CurrentUser is null)
        {
            throw new InvalidOperationException(
                "There is no signed-in account to attach an email to. Call EnsureSignedInAsync first.");
        }

        await auth.Update(new UserAttributes { Email = email }).ConfigureAwait(false);

        _logger.LogInformation("An email was submitted for the current account; it takes effect once confirmed");
    }

    public async Task<User> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        IGotrueClient<User, Session> auth = await AuthAsync(cancellationToken).ConfigureAwait(false);

        Session session = await auth.SignInWithPassword(email, password).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Signing in returned no session.");

        return session.User ?? throw new InvalidOperationException("Signing in returned a session with no user.");
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        IGotrueClient<User, Session> auth = await AuthAsync(cancellationToken).ConfigureAwait(false);

        await auth.SignOut().ConfigureAwait(false);
    }

    /// <summary>
    /// The auth client, with this listening to it.
    /// </summary>
    /// <remarks>
    /// Subscribed on first use rather than in the constructor: there is no client to subscribe to
    /// until one has been initialized, and building it is what a caller came here to trigger.
    /// </remarks>
    private async Task<IGotrueClient<User, Session>> AuthAsync(CancellationToken cancellationToken)
    {
        IGotrueClient<User, Session> auth =
            (await _clients.InitializeAsync(cancellationToken).ConfigureAwait(false)).Auth;

        if (ReferenceEquals(_listening, auth)) return auth;

        _listening = auth;
        auth.AddStateChangedListener(OnAuthStateChanged);

        return auth;
    }

    private void OnAuthStateChanged(IGotrueClient<User, Session> sender, AuthState state)
    {
        // Only what changes who the application is acting as. A refreshed token is the same user
        // with a newer credential, and waking every listener for it would be noise.
        if (state is not (AuthState.SignedIn or AuthState.SignedOut or AuthState.UserUpdated)) return;

        Changed?.Invoke(this, new SupabaseAuthChange(sender.CurrentUser, sender.CurrentUser is not null));
    }
}
