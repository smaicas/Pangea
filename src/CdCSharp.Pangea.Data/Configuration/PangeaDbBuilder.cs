using CdCSharp.Pangea.Data.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Data.Configuration;

/// <summary>
/// What <c>AddPangeaDbContext</c> hands the application to describe one database with.
/// </summary>
/// <remarks>
/// The provider is chosen by an extension method a provider package puts on this type -
/// <c>UseSqlite()</c> lives in <c>CdCSharp.Pangea.Data.Sqlite</c> - which is what keeps the engines
/// out of the feature: the method does not exist until the package is installed, and installing it
/// is the only thing that brings a driver with it.
/// </remarks>
public sealed class PangeaDbBuilder
{
    internal PangeaDbBuilder(IServiceCollection services, Type contextType)
    {
        Services = services;
        ContextType = contextType;
    }

    /// <summary>The container being built, for a provider that needs services of its own.</summary>
    public IServiceCollection Services { get; }

    /// <summary>The context this describes.</summary>
    public Type ContextType { get; }

    /// <summary>Where the database goes, how it migrates, how it logs.</summary>
    public PangeaDbOptions Options { get; } = new();

    /// <summary>The engine, once a provider package has named one.</summary>
    public IPangeaDbProvider? Provider { get; private set; }

    /// <summary>Names the engine. Called by a provider package's <c>Use...</c> method.</summary>
    /// <exception cref="InvalidOperationException">A provider was already chosen for this context.</exception>
    public PangeaDbBuilder UseProvider(IPangeaDbProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (Provider is not null)
        {
            throw new InvalidOperationException(
                $"'{ContextType.Name}' is already using the {Provider.Name} provider. A context has one engine; " +
                "register a second context to talk to a second database.");
        }

        Provider = provider;
        return this;
    }
}
