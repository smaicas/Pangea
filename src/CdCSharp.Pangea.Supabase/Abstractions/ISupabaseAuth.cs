using Supabase.Gotrue;
using static Supabase.Gotrue.Constants;

namespace CdCSharp.Pangea.Supabase.Abstractions;

/// <summary>Who the application is signed in as, and how that changes.</summary>
public interface ISupabaseAuth
{
    /// <summary>The signed-in user, or <see langword="null"/>.</summary>
    User? CurrentUser { get; }

    /// <summary>The signed-in user's id, or <see langword="null"/>.</summary>
    /// <remarks>
    /// The value every row-level-security policy is written against, so it is what an application's
    /// own tables store as an author or an owner.
    /// </remarks>
    string? UserId { get; }

    /// <summary>Whether there is a session at all.</summary>
    bool IsSignedIn { get; }

    /// <summary>
    /// Whether the session belongs to an anonymous account.
    /// </summary>
    /// <remarks>
    /// True until an email is attached. Worth surfacing somewhere quiet in the UI: the account
    /// lives only in this installation's stored session, so losing the device loses the data.
    /// </remarks>
    bool IsAnonymous { get; }

    /// <summary>Raised whenever the signed-in user changes, including on sign-out.</summary>
    /// <remarks>Raised on whatever thread the change arrived on, which is not the UI thread.</remarks>
    event EventHandler<SupabaseAuthChange>? Changed;

    /// <summary>
    /// Signs in anonymously, and does nothing when there is already a session.
    /// </summary>
    /// <remarks>
    /// The account is real - it owns rows, and policies apply to it - and it is reachable only
    /// through the refresh token this installation stored. Attaching an email later keeps the same
    /// account and everything in it.
    /// </remarks>
    Task<User> EnsureSignedInAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches an email to the account that is already signed in, so it survives this device.
    /// </summary>
    /// <remarks>
    /// The user has to confirm it from their inbox. Until they do, the account is still the
    /// anonymous one and <see cref="IsAnonymous"/> still says so.
    /// </remarks>
    Task LinkEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>The providers attached to this account, empty when there are none.</summary>
    /// <remarks>
    /// What an account settings screen shows: an anonymous account has nothing here, and one that
    /// has been through <see cref="StartLinkAsync"/> has the provider it was linked to.
    /// </remarks>
    IReadOnlyList<UserIdentity> Identities { get; }

    /// <summary>
    /// Begins attaching a provider to the account that is already signed in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Linking rather than signing in, and the distinction is the whole point: an anonymous account
    /// owns rows, and signing in with Google would move the user to a different account with none
    /// of them. This keeps the account and gives it a second way in, so the same data opens on a
    /// second device.
    /// </para>
    /// <para>
    /// Two steps because the middle of it happens outside the application. Open the returned
    /// address - a browser, a custom tab - and hand whatever the provider redirects back to
    /// <see cref="CompleteLinkAsync"/>. Nothing about the account changes until that lands.
    /// </para>
    /// </remarks>
    /// <param name="provider">Which provider to attach.</param>
    /// <param name="cancellationToken">Stops waiting for the address to be prepared.</param>
    /// <param name="redirectTo">
    /// Where the provider sends the user back to. It has to be a URL the platform routes to this
    /// application - a registered custom scheme on a phone - and it has to be listed as a redirect
    /// URL on the Supabase project, or the provider refuses it.
    /// </param>
    Task<Uri> StartLinkAsync(
        Provider provider, string redirectTo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finishes a link from the address the provider redirected back to.
    /// </summary>
    /// <remarks>
    /// The verifier that pairs with the code in that address is held in memory only, so a link
    /// begun before the process was killed cannot be finished - the user starts it again, which is
    /// one tap, and the alternative is writing the other half of a credential to disk.
    /// </remarks>
    Task<User> CompleteLinkAsync(Uri callback, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attaches a provider from an id token a platform sign-in SDK already obtained.
    /// </summary>
    /// <remarks>
    /// The same result as <see cref="StartLinkAsync"/> with no browser in the middle, which is what
    /// Google Sign-In on Android and Sign in with Apple on iOS are for. The token's audience has to
    /// be a client id the Supabase project accepts.
    /// </remarks>
    Task<User> LinkWithIdTokenAsync(
        Provider provider, string idToken, string? nonce = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detaches a provider from the account.
    /// </summary>
    /// <remarks>
    /// Refused by the server when it is the only way left into the account, which is the behaviour
    /// worth having: an account with nothing attached is reachable through its stored session and
    /// nowhere else.
    /// </remarks>
    Task UnlinkAsync(Provider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs in with an email and password, replacing whatever session there was.
    /// </summary>
    Task<User> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the session and forgets it.
    /// </summary>
    /// <remarks>
    /// On an anonymous account this is not a sign-out but a deletion: nothing else can ever reach
    /// that account again. Ask first.
    /// </remarks>
    Task SignOutAsync(CancellationToken cancellationToken = default);
}

/// <summary>What changed about the signed-in user.</summary>
/// <param name="User">Who is signed in now, or <see langword="null"/> after a sign-out.</param>
/// <param name="IsSignedIn">Whether there is a session.</param>
public sealed record SupabaseAuthChange(User? User, bool IsSignedIn);
