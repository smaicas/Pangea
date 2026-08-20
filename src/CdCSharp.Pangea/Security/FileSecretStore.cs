using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Storage.Abstractions;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace CdCSharp.Pangea.Security;

/// <summary>
/// The secret store an application gets without doing anything, and the best one available without
/// the platform's own API.
/// </summary>
/// <remarks>
/// <para>
/// One file per secret, in a <c>secrets</c> folder inside the application's data directory, named
/// after a hash of the key so that the key itself - which is often the account it belongs to - is
/// not sitting in a directory listing.
/// </para>
/// <para>
/// On Windows the bytes are encrypted with DPAPI under the current user, so a copy of the file is
/// useless on another machine or to another account. Everywhere else they are written with
/// permissions that keep other accounts out, which is what the filesystem alone can offer. Neither
/// protects against something already running as the user: for that an application needs the
/// platform's keystore, and registering an <see cref="ISecretStore"/> of its own is how it gets it.
/// </para>
/// </remarks>
internal sealed class FileSecretStore : ISecretStore
{
    private const string Folder = "secrets";
    private const string Extension = ".secret";

    private readonly IStorageService _storage;
    private readonly ILogger<FileSecretStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileSecretStore(IStorageService storage, ILogger<FileSecretStore> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public SecretProtection Protection =>
        OperatingSystem.IsWindows() ? SecretProtection.OperatingSystem : SecretProtection.UserOnly;

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        string path = PathFor(key);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!File.Exists(path)) return null;

            byte[] stored = await File.ReadAllBytesAsync(path, cancellationToken);
            byte[] plain = Unprotect(stored, path);

            try
            {
                return Encoding.UTF8.GetString(plain);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plain);
            }
        }
        catch (CryptographicException ex)
        {
            // Written by another user or on another machine - a copied profile, a restored backup.
            // Treated as absent: the application signs in again, which is the recovery it already
            // has for a first run.
            _logger.LogWarning(ex, "A stored secret could not be decrypted and is being ignored");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(secret);

        string path = PathFor(key);
        byte[] plain = Encoding.UTF8.GetBytes(secret);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            await File.WriteAllBytesAsync(path, Protect(plain), cancellationToken);

            Restrict(path);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            _gate.Release();
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        string path = PathFor(key);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static byte[] Protect(byte[] plain) =>
        OperatingSystem.IsWindows()
            ? ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser)
            : plain;

    private byte[] Unprotect(byte[] stored, string path)
    {
        if (!OperatingSystem.IsWindows()) return stored;

        try
        {
            return ProtectedData.Unprotect(stored, optionalEntropy: null, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            _logger.LogWarning("The secret at {Path} is not readable by this user account", path);
            throw;
        }
    }

    /// <summary>
    /// Keeps other accounts out of the file.
    /// </summary>
    /// <remarks>
    /// Unix only. On Windows the data directory is already inside the user's profile and the bytes
    /// are encrypted anyway, and rewriting ACLs there is a good way to lock an application out of
    /// its own file.
    /// </remarks>
    private void Restrict(string path)
    {
        if (OperatingSystem.IsWindows()) return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Worth knowing about: the secret is stored, and less privately than intended.
            _logger.LogWarning(ex, "The permissions of {Path} could not be restricted to this user", path);
        }
    }

    /// <summary>
    /// The file a key lives in: a hash, so the key is not readable from the directory itself and
    /// anything at all can be used as one.
    /// </summary>
    private string PathFor(string key)
    {
        string name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

        return _storage.GetDataFilePath(Path.Combine(Folder, name + Extension));
    }
}
