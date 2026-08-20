using CdCSharp.Pangea.Storage.Abstractions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace CdCSharp.Pangea.Supabase.Services;

/// <summary>
/// Keeps the signed-in session in the per-platform data directory, so it survives a restart.
/// </summary>
/// <remarks>
/// <para>
/// Written with plain file calls rather than through <c>IStorageService</c>'s asynchronous methods:
/// Gotrue's persistence contract is synchronous, and blocking on a task from inside it is how a UI
/// thread deadlocks. The path still comes from the storage feature, so portable mode and the
/// per-platform directories are all respected.
/// </para>
/// <para>
/// The file holds a refresh token. See <see cref="SupabaseOptions.SessionFileName"/> for what that
/// does and does not protect.
/// </para>
/// </remarks>
internal sealed class StorageSessionPersistence : IGotrueSessionPersistence<Session>
{
    private readonly string _path;
    private readonly ILogger _logger;

    public StorageSessionPersistence(IStorageService storage, string fileName, ILogger logger)
    {
        _path = storage.GetDataFilePath(fileName);
        _logger = logger;
    }

    public void SaveSession(Session session)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_path);

            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            File.WriteAllText(_path, JsonConvert.SerializeObject(session));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The session is still live in memory; what is lost is the next launch starting signed
            // in. Failing the sign-in over it would be the worse trade.
            _logger.LogError(ex, "The Supabase session could not be saved to {Path}", _path);
        }
    }

    /// <summary>
    /// The stored session, or <see langword="null"/> when there is none to restore.
    /// </summary>
    /// <remarks>
    /// A file that cannot be read or parsed is treated as absent rather than thrown over: the
    /// application signs in again, which is the same recovery it needs on a first run.
    /// </remarks>
    public Session? LoadSession()
    {
        try
        {
            return File.Exists(_path) ? JsonConvert.DeserializeObject<Session>(File.ReadAllText(_path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "The stored Supabase session at {Path} could not be read; signing in again", _path);
            return null;
        }
    }

    public void DestroySession()
    {
        try
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Worth shouting about: a session file that outlives a sign-out is a credential left on
            // disk after the user asked for it to be gone.
            _logger.LogError(ex, "The Supabase session at {Path} could not be deleted", _path);
        }
    }
}
