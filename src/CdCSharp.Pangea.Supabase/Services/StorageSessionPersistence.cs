using CdCSharp.Pangea.Storage.Abstractions;
using Microsoft.Extensions.Logging;

namespace CdCSharp.Pangea.Supabase.Services;

/// <summary>
/// Keeps the signed-in session in a file in the per-platform data directory, so it survives a
/// restart. The fallback for a container with no <c>ISecretStore</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every read and write goes through <see cref="IStorageService"/>, which is what the storage
/// feature is for: portable mode, the per-platform directories, and a test's in-memory storage are
/// all respected. Going to the disk directly at the path the service names would write a real file
/// wherever that path happens to point - for the test double, the root of the current drive, shared
/// by every container in the process, so that every one of them signed in as the same account.
/// </para>
/// <para>
/// The service is asynchronous and Gotrue's contract is not, so the session is read by
/// <see cref="PrimeAsync"/> before the client is built and written behind the caller afterwards:
/// see <see cref="PrimedSessionPersistence"/>.
/// </para>
/// <para>
/// The file holds a refresh token. See <see cref="SupabaseOptions.SessionFileName"/> for what that
/// does and does not protect.
/// </para>
/// </remarks>
internal sealed class StorageSessionPersistence : PrimedSessionPersistence
{
    private readonly IStorageService _storage;
    private readonly string _path;

    public StorageSessionPersistence(IStorageService storage, string fileName, ILogger logger) : base(logger)
    {
        _storage = storage;
        _path = storage.GetDataFilePath(fileName);
    }

    /// <summary>Reads the stored session, if there is one, before the client asks for it.</summary>
    /// <remarks>
    /// A file that cannot be read is treated as absent rather than thrown over: the application
    /// signs in again, which is the same recovery it needs on a first run.
    /// </remarks>
    public async Task PrimeAsync(CancellationToken cancellationToken = default)
    {
        string? stored = null;

        try
        {
            if (_storage.FileExists(_path))
            {
                stored = await _storage.ReadTextAsync(_path).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogWarning(ex, "The stored Supabase session at {Path} could not be read; signing in again", _path);
        }

        Prime(stored);
    }

    protected override Task WriteAsync(string session, CancellationToken cancellationToken) =>
        _storage.WriteTextAsync(_path, session);

    protected override Task RemoveAsync(CancellationToken cancellationToken)
    {
        if (_storage.FileExists(_path)) _storage.DeleteFile(_path);

        return Task.CompletedTask;
    }
}
