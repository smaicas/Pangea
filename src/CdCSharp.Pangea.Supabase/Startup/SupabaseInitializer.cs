using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Supabase.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Supabase.Startup;

/// <summary>
/// Reaches the project while the splash is up, and signs in if the application asked for it.
/// </summary>
/// <remarks>
/// <para>
/// An initializer rather than work started and forgotten, because the first screen of an
/// application with a shared backend usually has a user's name on it. What it is not is a
/// precondition: by default a backend that cannot be reached is logged and startup carries on, so
/// an application with a local cache opens on a train.
/// </para>
/// <para>
/// Set <see cref="SupabaseOptions.RequireConnectionAtStartup"/> when the first screen genuinely has
/// nothing to draw without the server.
/// </para>
/// </remarks>
public sealed class SupabaseInitializer : IPangeaAsyncInitializer
{
    private readonly ISupabaseClientProvider _clients;
    private readonly ISupabaseAuth _auth;
    private readonly SupabaseOptions _options;
    private readonly ILogger<SupabaseInitializer> _logger;

    public SupabaseInitializer(
        ISupabaseClientProvider clients,
        ISupabaseAuth auth,
        IOptions<SupabaseOptions> options,
        ILogger<SupabaseInitializer> logger)
    {
        _clients = clients;
        _auth = auth;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "Connecting";

    /// <summary>
    /// Late, so anything the application has to do to its own data has already happened.
    /// </summary>
    public int Order => 100;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        // A cancellation of its own, so a slow network is given up on without giving up on the rest
        // of startup - the outer token stops everything, this one stops only the connection.
        using CancellationTokenSource attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        attempt.CancelAfter(_options.InitializeTimeout);

        try
        {
            await _clients.InitializeAsync(attempt.Token).ConfigureAwait(false);

            if (_options.SignInAnonymouslyOnStart)
            {
                await _auth.EnsureSignedInAsync(attempt.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            if (_options.RequireConnectionAtStartup) throw;

            _logger.LogWarning(
                ex, "Supabase could not be reached during startup; the application starts on what it has locally");
        }
    }
}
