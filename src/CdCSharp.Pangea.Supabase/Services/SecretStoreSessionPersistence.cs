using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

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
/// Gotrue's persistence contract is synchronous and <see cref="ISecretStore"/> is not, and blocking
/// on a task from inside a call the UI thread makes is how that thread deadlocks. So the session is
/// held in memory: read once before the client is built - which is what
/// <see cref="PrimeAsync"/> is for - and written behind the caller afterwards. Gotrue does not wait
/// for the write and has nothing to do if it fails; what matters is that the next launch finds it,
/// and the next launch is a long time after this returns.
/// </para>
/// </remarks>
internal sealed class SecretStoreSessionPersistence : IGotrueSessionPersistence<Session>
{
    private readonly ISecretStore _secrets;
    private readonly string _key;
    private readonly ILogger _logger;
    private readonly object _gate = new();

    private string? _session;
    private Task _pending = Task.CompletedTask;

    public SecretStoreSessionPersistence(ISecretStore secrets, string key, ILogger logger)
    {
        _secrets = secrets;
        _key = key;
        _logger = logger;
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
    /// import entirely.
    /// </para>
    /// </remarks>
    public async Task PrimeAsync(string? legacyFilePath, CancellationToken cancellationToken = default)
    {
        string? stored = await _secrets.GetAsync(_key, cancellationToken).ConfigureAwait(false);

        if (stored is null && legacyFilePath is not null)
        {
            stored = await AdoptAsync(legacyFilePath, cancellationToken).ConfigureAwait(false);
        }

        lock (_gate)
        {
            _session = stored;
        }
    }

    private async Task<string?> AdoptAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path)) return null;

            string contents = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

            await _secrets.SetAsync(_key, contents, cancellationToken).ConfigureAwait(false);

            File.Delete(path);

            _logger.LogInformation(
                "The Supabase session was moved out of {Path} and into the {Protection} secret store",
                path, _secrets.Protection);

            return contents;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "The Supabase session at {Path} could not be moved into the secret store", path);
            return null;
        }
    }

    public Session? LoadSession()
    {
        string? stored;

        lock (_gate)
        {
            stored = _session;
        }

        if (string.IsNullOrEmpty(stored)) return null;

        try
        {
            return JsonConvert.DeserializeObject<Session>(stored);
        }
        catch (JsonException ex)
        {
            // Treated as absent rather than thrown over: the application signs in again, which is
            // the same recovery it needs on a first run.
            _logger.LogWarning(ex, "The stored Supabase session could not be read; signing in again");
            return null;
        }
    }

    public void SaveSession(Session session)
    {
        string serialized = JsonConvert.SerializeObject(session);

        lock (_gate)
        {
            _session = serialized;
        }

        Queue(token => _secrets.SetAsync(_key, serialized, token), "saved");
    }

    public void DestroySession()
    {
        lock (_gate)
        {
            _session = null;
        }

        Queue(token => _secrets.RemoveAsync(_key, token), "removed");
    }

    /// <summary>
    /// Runs a store operation behind the caller, one at a time and in order.
    /// </summary>
    /// <remarks>
    /// Chained rather than fired off: a save and the delete that follows it racing each other is
    /// how a signed-out application starts up signed in.
    /// </remarks>
    private void Queue(Func<CancellationToken, Task> operation, string what)
    {
        lock (_gate)
        {
            _pending = _pending.ContinueWith(
                async _ =>
                {
                    try
                    {
                        await operation(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // The session is still live in memory; what is lost is the next launch
                        // starting signed in. Failing the sign-in over it would be the worse trade.
                        _logger.LogError(ex, "The Supabase session could not be {What}", what);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).Unwrap();
        }
    }

    /// <summary>Waits for the writes queued behind the caller. For tests and for a clean shutdown.</summary>
    public Task DrainAsync()
    {
        lock (_gate)
        {
            return _pending;
        }
    }
}
