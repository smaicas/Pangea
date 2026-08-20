using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace PangeaSupabaseApp.Services;

/// <summary>
/// Works out who this is, and sends anything written while the application was closed.
/// </summary>
/// <remarks>
/// <para>
/// After the Supabase feature's own initializer, which is what made the session this reads. Both
/// run behind the splash, so the first screen can ask who is signed in and get an answer rather
/// than a null it has to redraw around.
/// </para>
/// <para>
/// Neither half is allowed to stop the application: a phone that starts with no signal shows what
/// it had and syncs when it can.
/// </para>
/// </remarks>
public sealed class StartupInitializer : IPangeaAsyncInitializer
{
    private readonly NotesRepository _repository;
    private readonly ILogger<StartupInitializer> _logger;

    public StartupInitializer(NotesRepository repository, ILogger<StartupInitializer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public string Name => "Signing in";

    /// <summary>After the backend has connected, which is what this reads a session from.</summary>
    public int Order => 200;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _repository.SignInAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Signing in failed; the application starts on what it has locally");
            return;
        }

        try
        {
            int sent = await _repository.SyncAsync(cancellationToken).ConfigureAwait(false);

            if (sent > 0) _logger.LogInformation("{Count} queued writes were sent at startup", sent);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The queue is still there. It goes out on the next launch, or when the user syncs.
            _logger.LogWarning(ex, "Queued writes could not be sent at startup");
        }
    }
}
