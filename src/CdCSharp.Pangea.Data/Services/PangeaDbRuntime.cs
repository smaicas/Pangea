using CdCSharp.Pangea.Data.Abstractions;
using CdCSharp.Pangea.Data.Configuration;
using Microsoft.EntityFrameworkCore;

namespace CdCSharp.Pangea.Data.Services;

/// <summary>
/// What one registered context is, once the application has finished starting: its options, its
/// provider, the connection string those two worked out, and the lock its writes queue behind.
/// </summary>
/// <remarks>
/// A singleton, so the connection string is resolved once - it touches the filesystem to make sure
/// the folder exists - and so the write lock is the same one for every caller. Generic over the
/// context type because an application can register more than one database and they share nothing.
/// </remarks>
internal sealed class PangeaDbRuntime<TContext> where TContext : DbContext
{
    public PangeaDbRuntime(PangeaDbOptions options, IPangeaDbProvider provider, IDatabaseLocator locator)
    {
        Options = options;
        Provider = provider;
        Locator = locator;
        ConnectionString = provider.ResolveConnectionString(options, locator);
        WriteLock = provider.SerializesWrites ? new SemaphoreSlim(1, 1) : null;
    }

    public PangeaDbOptions Options { get; }

    public IPangeaDbProvider Provider { get; }

    public IDatabaseLocator Locator { get; }

    public string ConnectionString { get; }

    /// <summary>Null when the engine handles concurrent writers itself.</summary>
    public SemaphoreSlim? WriteLock { get; }

    /// <summary>The file the database lives in, or null when it lives on a server.</summary>
    public string? DatabaseFilePath => Provider.GetDatabaseFilePath(ConnectionString);
}
