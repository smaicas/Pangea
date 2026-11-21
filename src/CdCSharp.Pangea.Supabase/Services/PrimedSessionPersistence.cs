using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace CdCSharp.Pangea.Supabase.Services;

/// <summary>
/// A session read before the client is built, held in memory, and written behind the caller.
/// </summary>
/// <remarks>
/// <para>
/// Gotrue's persistence contract is synchronous and every store the session can live in - the
/// secret store, <c>IStorageService</c> - is not, and blocking on a task from inside a call the UI
/// thread makes is how that thread deadlocks. So the session is read once, before Gotrue asks for
/// it, and every later change is answered from memory and written afterwards. Gotrue does not wait
/// for the write and has nothing to do if it fails; what matters is that the next launch finds it,
/// and the next launch is a long time after this returns.
/// </para>
/// <para>
/// The subclass decides where the session is kept; this decides when it is read and written.
/// </para>
/// </remarks>
internal abstract class PrimedSessionPersistence : IGotrueSessionPersistence<Session>
{
    private readonly object _gate = new();

    private string? _session;
    private Task _pending = Task.CompletedTask;

    protected PrimedSessionPersistence(ILogger logger) => Logger = logger;

    protected ILogger Logger { get; }

    /// <summary>Writes the serialized session where it is kept.</summary>
    protected abstract Task WriteAsync(string session, CancellationToken cancellationToken);

    /// <summary>Removes the session from where it is kept.</summary>
    protected abstract Task RemoveAsync(CancellationToken cancellationToken);

    /// <summary>What the subclass read before the client was built: the session Gotrue will load.</summary>
    protected void Prime(string? stored)
    {
        lock (_gate)
        {
            _session = stored;
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
            Logger.LogWarning(ex, "The stored Supabase session could not be read; signing in again");
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

        Queue(token => WriteAsync(serialized, token), "saved");
    }

    public void DestroySession()
    {
        lock (_gate)
        {
            _session = null;
        }

        // Worth shouting about if it fails: a session that outlives a sign-out is a credential left
        // behind after the user asked for it to be gone.
        Queue(RemoveAsync, "removed");
    }

    /// <summary>Waits for the writes queued behind the caller. For tests and for a clean shutdown.</summary>
    public Task DrainAsync()
    {
        lock (_gate)
        {
            return _pending;
        }
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
                        Logger.LogError(ex, "The Supabase session could not be {What}", what);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).Unwrap();
        }
    }
}
