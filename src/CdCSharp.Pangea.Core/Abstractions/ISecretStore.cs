namespace CdCSharp.Pangea.Core.Abstractions;

/// <summary>How well the store behind an <see cref="ISecretStore"/> protects what it holds.</summary>
/// <remarks>
/// Reported rather than assumed, because it differs by platform and an application may need to
/// know: whether to offer "stay signed in" at all, what to say about it, and how long a session is
/// allowed to live.
/// </remarks>
public enum SecretProtection
{
    /// <summary>A file anything running as this user can read. The last resort, never the default.</summary>
    None,

    /// <summary>
    /// A file the operating system restricts to this user account. Stops another account reading
    /// it; stops nothing running as the user.
    /// </summary>
    UserOnly,

    /// <summary>
    /// Encrypted with a key the operating system holds for this user, so the bytes on disk are
    /// useless on another machine or to another account. Windows DPAPI is this.
    /// </summary>
    OperatingSystem,

    /// <summary>
    /// Held by the platform's own secret store, backed by hardware where the device has it: the
    /// Android Keystore, the iOS and macOS Keychain.
    /// </summary>
    Device
}

/// <summary>
/// Where a credential goes.
/// </summary>
/// <remarks>
/// <para>
/// Tokens do not belong in the settings file. A refresh token is a bearer credential: whoever has
/// it is the user until it is revoked, and <c>settings.json</c> is copied into backups, sync
/// folders, support bundles and screenshots without anyone thinking about it.
/// </para>
/// <para>
/// The toolkit registers a store that protects secrets as well as it can without the platform's own
/// APIs - which on Windows is real encryption and elsewhere is a file only this user can read.
/// <see cref="Protection"/> says which. An Android or iOS head that can reach the Keystore or the
/// Keychain registers its own implementation over the top, and everything that stores a secret
/// picks it up with no change.
/// </para>
/// </remarks>
public interface ISecretStore
{
    /// <summary>What this store gives whatever it holds. Constant for the life of the application.</summary>
    SecretProtection Protection { get; }

    /// <summary>The secret stored under <paramref name="key"/>, or null if there is none.</summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Stores <paramref name="secret"/>, replacing whatever was under that key.</summary>
    Task SetAsync(string key, string secret, CancellationToken cancellationToken = default);

    /// <summary>Removes the secret under <paramref name="key"/>. Removing one that is not there is not an error.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
