using Android.Content;
using Android.Security.Keystore;
using CdCSharp.Pangea.Core.Abstractions;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;

namespace PangeaMobileApp.Android;

/// <summary>
/// Secrets in the Android Keystore, which is hardware-backed where the device has the hardware.
/// </summary>
/// <remarks>
/// <para>
/// The key never leaves the Keystore: what is stored in preferences is ciphertext and the nonce
/// that produced it, and neither is worth anything on another device or to another application.
/// This is what <see cref="SecretProtection.Device"/> means.
/// </para>
/// <para>
/// Registered by the head, over the default the toolkit put in - the last registration wins, which
/// is how a platform head contributes something only it can reach.
/// </para>
/// </remarks>
public sealed class KeystoreSecretStore : ISecretStore
{
    private const string KeyAlias = "PangeaMobileApp.secrets";
    private const string Transformation = "AES/GCM/NoPadding";
    private const int NonceBytes = 12;
    private const int TagBits = 128;

    private readonly ISharedPreferences _preferences;

    public KeystoreSecretStore(Context context) =>
        _preferences = context.GetSharedPreferences("pangea.secrets", FileCreationMode.Private)
                       ?? throw new InvalidOperationException("Shared preferences are not available.");

    public SecretProtection Protection => SecretProtection.Device;

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        string? stored = _preferences.GetString(key, null);

        if (stored is null) return Task.FromResult<string?>(null);

        try
        {
            byte[] blob = Convert.FromBase64String(stored);

            // The nonce is prepended: it is not a secret, and it has to travel with the ciphertext
            // because GCM will not decrypt without the exact one that encrypted it.
            byte[] nonce = blob[..NonceBytes];
            byte[] cipherText = blob[NonceBytes..];

            Cipher cipher = Cipher.GetInstance(Transformation)!;
            cipher.Init(CipherMode.DecryptMode, Key(), new GCMParameterSpec(TagBits, nonce));

            byte[] plain = cipher.DoFinal(cipherText)!;

            return Task.FromResult<string?>(System.Text.Encoding.UTF8.GetString(plain));
        }
        catch (Exception)
        {
            // A secret that will not decrypt is gone: the key was replaced, or the backup came from
            // another device. Treated as absent, which sends the application through signing in
            // again rather than through a crash on launch.
            _preferences.Edit()?.Remove(key)?.Apply();

            return Task.FromResult<string?>(null);
        }
    }

    public Task SetAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        Cipher cipher = Cipher.GetInstance(Transformation)!;
        cipher.Init(CipherMode.EncryptMode, Key());

        byte[] cipherText = cipher.DoFinal(System.Text.Encoding.UTF8.GetBytes(secret))!;
        byte[] nonce = cipher.GetIV()!;

        byte[] blob = [.. nonce, .. cipherText];

        _preferences.Edit()?.PutString(key, Convert.ToBase64String(blob))?.Apply();

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _preferences.Edit()?.Remove(key)?.Apply();

        return Task.CompletedTask;
    }

    /// <summary>The key for this application, generated once and never exported.</summary>
    private static IKey Key()
    {
        KeyStore store = KeyStore.GetInstance("AndroidKeyStore")!;
        store.Load(null);

        if (store.GetKey(KeyAlias, null) is { } existing) return existing;

        KeyGenerator generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, "AndroidKeyStore")!;

        generator.Init(new KeyGenParameterSpec.Builder(KeyAlias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)!
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)!
            .Build()!);

        return generator.GenerateKey()!;
    }
}
