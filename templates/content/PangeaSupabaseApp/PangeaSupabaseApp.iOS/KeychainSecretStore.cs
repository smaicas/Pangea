using CdCSharp.Pangea.Core.Abstractions;
using Foundation;
using Security;

namespace PangeaSupabaseApp.iOS;

/// <summary>
/// Secrets in the iOS Keychain.
/// </summary>
/// <remarks>
/// <para>
/// <c>AfterFirstUnlock</c> rather than <c>WhenUnlocked</c>: an application that refreshes a token in
/// the background needs to read it while the phone is in a pocket, and the stricter accessibility
/// would fail those reads and sign the user out for no reason they could see.
/// </para>
/// <para>
/// Not synchronised to iCloud. A session belongs to the device that signed in - restoring it onto a
/// second phone would put two devices on one refresh token, which the server treats as theft.
/// </para>
/// </remarks>
public sealed class KeychainSecretStore : ISecretStore
{
    private const string Service = "PangeaSupabaseApp";

    public SecretProtection Protection => SecretProtection.Device;

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        SecRecord query = Query(key);

        NSData? found = SecKeyChain.QueryAsData(query, false, out SecStatusCode status);

        return Task.FromResult(status is SecStatusCode.Success && found is not null
            ? NSString.FromData(found, NSStringEncoding.UTF8)?.ToString()
            : null);
    }

    public Task SetAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        // Removed first: SecKeyChain has no upsert, and Add on an existing item returns
        // DuplicateItem rather than replacing it.
        SecKeyChain.Remove(Query(key));

        SecRecord record = Query(key);
        record.ValueData = NSData.FromString(secret, NSStringEncoding.UTF8);

        SecStatusCode status = SecKeyChain.Add(record);

        if (status is not SecStatusCode.Success)
        {
            throw new InvalidOperationException($"The Keychain refused to store '{key}': {status}.");
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        // Removing what is not there answers NoSuchKeyChain, which is not a failure.
        SecKeyChain.Remove(Query(key));

        return Task.CompletedTask;
    }

    private static SecRecord Query(string key) => new(SecKind.GenericPassword)
    {
        Service = Service,
        Account = key,
        Accessible = SecAccessible.AfterFirstUnlock
    };
}
