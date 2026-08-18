using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Services;

/// <summary>
/// Finds the <see cref="IPangeaFeature"/> implementations available to the application, lets each
/// one register its services, and later hands each one the running application to configure.
/// </summary>
/// <remarks>
/// <para>
/// One instance per application, built during startup and registered in the container.
/// </para>
/// <para>
/// Discovery goes through <see cref="TypeRegistry"/>, so there is a single assembly scan. A feature
/// that fails to configure aborts startup: half a feature is worse than none, and a silent failure
/// here surfaces much later as a missing service.
/// </para>
/// </remarks>
public class FeatureRegistry
{
    private readonly TypeRegistry _typeRegistry;
    private readonly PangeaCatalogIndex _catalog;
    private readonly List<IPangeaFeature> _features = [];

    /// <param name="typeRegistry">Where features are found when nothing was generated.</param>
    /// <param name="catalog">
    /// The generated catalogs. When they carry features, each one is built by a constructor call
    /// the compiler wrote rather than by <c>Activator</c>, and no assembly is read to find them.
    /// </param>
    public FeatureRegistry(TypeRegistry typeRegistry, PangeaCatalogIndex? catalog = null)
    {
        _typeRegistry = typeRegistry ?? throw new ArgumentNullException(nameof(typeRegistry));
        _catalog = catalog ?? PangeaCatalogIndex.Empty;
    }

    /// <summary>Features discovered so far, in discovery order.</summary>
    public IReadOnlyList<IPangeaFeature> Features => _features;

    /// <summary>
    /// Instantiates every feature and lets it contribute services to the container.
    /// </summary>
    public void DiscoverAndRegister(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (IPangeaFeature instance in Discover())
        {
            if (_features.Any(feature => feature.GetType() == instance.GetType())) continue;

            instance.ConfigureServices(services);
            _features.Add(instance);
        }
    }

    /// <summary>
    /// Every feature available, in a stable order so services are registered the same way twice.
    /// </summary>
    private IEnumerable<IPangeaFeature> Discover()
    {
        if (!_catalog.IsEmpty)
        {
            return _catalog.Features
                .Select(build => build())
                .OrderBy(feature => feature.GetType().FullName, StringComparer.Ordinal);
        }

        return _typeRegistry.GetTypesImplementing<IPangeaFeature>()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(Create);
    }

    /// <summary>
    /// Runs each feature's application-level configuration, once the container is built.
    /// </summary>
    public void ConfigureApplication(IServiceProvider serviceProvider, IPangeaApplicationContext applicationContext)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(applicationContext);

        foreach (IPangeaFeature feature in _features)
        {
            try
            {
                feature.ConfigureApplication(serviceProvider, applicationContext);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Feature '{feature.Name}' ({feature.GetType().FullName}) failed to configure the application.", ex);
            }
        }
    }

    private static IPangeaFeature Create(Type featureType)
    {
        try
        {
            return (IPangeaFeature)Activator.CreateInstance(featureType)!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Feature '{featureType.FullName}' could not be created. Features need a public parameterless constructor.",
                ex);
        }
    }
}
