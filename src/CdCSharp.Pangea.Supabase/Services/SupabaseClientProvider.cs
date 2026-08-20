using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Supabase.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Supabase;
using Supabase.Gotrue.Interfaces;
using ClientOptions = Supabase.SupabaseOptions;

namespace CdCSharp.Pangea.Supabase.Services;

/// <inheritdoc cref="ISupabaseClientProvider"/>
internal sealed class SupabaseClientProvider : ISupabaseClientProvider
{
    private readonly SupabaseOptions _options;
    private readonly IStorageService _storage;
    private readonly ISecretStore? _secrets;
    private readonly ILogger<SupabaseClientProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Client? _client;

    /// <remarks>
    /// The secret store is optional only so that this feature can be used outside a Pangea
    /// application, which registers one. Without it the session falls back to the plain file it
    /// used to live in, and says so in the log.
    /// </remarks>
    public SupabaseClientProvider(
        IOptions<SupabaseOptions> options,
        IStorageService storage,
        ILogger<SupabaseClientProvider> logger,
        ISecretStore? secrets = null)
    {
        _options = options.Value;
        _storage = storage;
        _secrets = secrets;
        _logger = logger;
    }

    public bool IsInitialized => _client is not null;

    public Client Client => _client ?? throw new InvalidOperationException(
        "The Supabase client has not been initialized yet. Startup initializes it; a caller reaching it before " +
        "that has to await ISupabaseClientProvider.InitializeAsync, or check IsInitialized first.");

    public async Task<Client> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_client is { } ready) return ready;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Checked again inside the gate: the caller that waited here is usually one whose whole
            // job was already done by the caller ahead of it.
            if (_client is { } built) return built;

            Validate();

            (ClientOptions options, IGotrueSessionPersistence<global::Supabase.Gotrue.Session>? persistence) =
                await BuildClientOptionsAsync(cancellationToken).ConfigureAwait(false);

            Client client = new(_options.Url, _options.AnonKey, options);

            await client.InitializeAsync().ConfigureAwait(false);

            // Attached here, and deliberately after InitializeAsync rather than through
            // ClientOptions.SessionHandler. The handler on the options is set and then never
            // consulted, and an attachment made before InitializeAsync is thrown away with the
            // auth client that call builds. Either way the stored session was read off the device
            // on every launch and handed to nobody, so every restart signed in as a new anonymous
            // account - which for an anonymous user is the loss of everything they had.
            if (persistence is not null)
            {
                client.Auth.SetPersistence(persistence);

                // LoadSession is what reads the store; RetrieveSessionAsync only refreshes a
                // session that is already in memory, and with nothing loaded it has nothing to do.
                // Calling the second without the first is why a stored session was read off the
                // device and then ignored on every single launch.
                client.Auth.LoadSession();

                if (client.Auth.CurrentSession is not null)
                {
                    await client.Auth.RetrieveSessionAsync().ConfigureAwait(false);
                }
            }

            _logger.LogInformation(
                "Supabase client initialized against {Url}, signed in as {UserId}",
                _options.Url, client.Auth.CurrentUser?.Id ?? "nobody");

            _client = client;
            return client;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <remarks>
    /// Asynchronous for one reason: the session is read out of the secret store before the client
    /// is built. Gotrue asks for it synchronously during its own initialization, and a store that
    /// has to be awaited cannot answer that from the UI thread without deadlocking - so it is read
    /// here, where waiting is allowed, and answered from memory afterwards.
    /// </remarks>
    private async Task<(ClientOptions Options, IGotrueSessionPersistence<global::Supabase.Gotrue.Session>? Persistence)>
        BuildClientOptionsAsync(CancellationToken cancellationToken)
    {
        ClientOptions options = new()
        {
            AutoConnectRealtime = _options.AutoConnectRealtime,
            AutoRefreshToken = _options.AutoRefreshToken,
            Schema = _options.Schema
        };

        if (!_options.PersistSession) return (options, null);

        string legacyPath = _storage.GetDataFilePath(_options.SessionFileName);

        if (_secrets is null)
        {
            _logger.LogWarning(
                "No ISecretStore is registered, so the Supabase session stays in {Path} as plain text. " +
                "A Pangea application registers one; this is a container built by hand.", legacyPath);

            StorageSessionPersistence fallback = new(_storage, _options.SessionFileName, _logger);

            options.SessionHandler = fallback;
            return (options, fallback);
        }

        SecretStoreSessionPersistence persistence = new(_secrets, _options.SessionSecretKey, _logger);

        await persistence.PrimeAsync(legacyPath, cancellationToken).ConfigureAwait(false);

        options.SessionHandler = persistence;
        return (options, persistence);
    }

    /// <summary>
    /// Says what is missing before the client turns it into a request that fails somewhere else.
    /// </summary>
    /// <remarks>
    /// An empty URL produces a connection error naming no host, and an empty key produces a 401 on
    /// the first query. Both read as a broken backend rather than as unconfigured options.
    /// </remarks>
    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(_options.Url))
        {
            throw new InvalidOperationException(
                "SupabaseOptions.Url is not set. Configure it in App.Configure with the project URL, as " +
                "https://<ref>.supabase.co.");
        }

        if (string.IsNullOrWhiteSpace(_options.AnonKey))
        {
            throw new InvalidOperationException(
                "SupabaseOptions.AnonKey is not set. Configure it in App.Configure with the project's anon key - " +
                "never the service_role key, which must not ship in a client.");
        }
    }
}
