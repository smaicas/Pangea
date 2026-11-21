using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Storage.Abstractions;
using Microsoft.Extensions.Logging;

namespace CdCSharp.Pangea.Supabase.Services;

/// <summary>
/// Keeps the signed-in session in the platform's secret store instead of a file.
/// </summary>
/// <remarks>
/// <para>
/// A session holds a refresh token, and a refresh token is a bearer credential: whoever has it is
/// the user until it is revoked. In a JSON file it travels into backups, sync folders, support
/// bundles and screenshots without anyone deciding that it should.
/// </para>
/// <para>
/// <see cref="ISecretStore"/> is asynchronous and Gotrue's contract is not, so the session is read
/// by <see cref="PrimeAsync"/> before the client is built and written behind the caller
/// afterwards: see <see cref="PrimedSessionPersistence"/>.
/// </para>
/// </remarks>
internal sealed class SecretStoreSessionPersistence : PrimedSessionPersistence
{
    private readonly ISecretStore _secrets;
    private readonly string _key;
    private readonly IStorageService _storage;

    /// <param name="secrets">Where the session is kept.</param>
    /// <param name="key">The key it is kept under.</param>
    /// <param name="storage">Where an older version's session file is looked for and deleted.</param>
    /// <param name="logger">Where failures to keep it are reported.</param>
    public SecretStoreSessionPersistence(ISecretStore secrets, string key, IStorageService storage, ILogger logger)
        : base(logger)
    {
        _secrets = secrets;
        _key = key;
        _storage = storage;
    }

    /// <summary>
    /// Reads the stored session, and adopts one left behind by an older version.
    /// </summary>
    /// <remarks>
    /// The plain file is imported and deleted rather than ignored: leaving it would sign every
    /// existing user out on upgrade, and - worse - leave the credential that signed them in sitting
    /// where it always was.
    /// <para>
    /// The legacy path is the file previous versions wrote, or <see langword="null"/> to skip the
    /// import entirely. It is read and deleted through <see cref="IStorageService"/>, like every
    /// other file the feature touches.
    /// </para>
    /// </remarks>
    public async Task PrimeAsync(string? legacyFilePath, CancellationToken cancellationToken = default)
    {
        string? stored = await _secrets.GetAsync(_key, cancellationToken).ConfigureAwait(false);

        if (stored is null && legacyFilePath is not null)
        {
            stored = await AdoptAsync(legacyFilePath, cancellationToken).ConfigureAwait(false);
        }

        Prime(stored);
    }

    protected override Task WriteAsync(string session, CancellationToken cancellationToken) =>
        _secrets.SetAsync(_key, session, cancellationToken);

    protected override Task RemoveAsync(CancellationToken cancellationToken) =>
        _secrets.RemoveAsync(_key, cancellationToken);

    private async Task<string?> AdoptAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!_storage.FileExists(path)) return null;

            string contents = await _storage.ReadTextAsync(path).WaitAsync(cancellationToken).ConfigureAwait(false);

            await _secrets.SetAsync(_key, contents, cancellationToken).ConfigureAwait(false);

            _storage.DeleteFile(path);

            Logger.LogInformation(
                "The Supabase session was moved out of {Path} and into the {Protection} secret store",
                path, _secrets.Protection);

            return contents;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogWarning(ex, "The Supabase session at {Path} could not be moved into the secret store", path);
            return null;
        }
    }
}
