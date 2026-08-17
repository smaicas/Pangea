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
    private readonly List<IPangeaFeature> _features = [];

    public FeatureRegistry(TypeRegistry typeRegistry) =>
        _typeRegistry = typeRegistry ?? throw new ArgumentNullException(nameof(typeRegistry));

    /// <summary>Features discovered so far, in discovery order.</summary>
    public IReadOnlyList<IPangeaFeature> Features => _features;

    /// <summary>
    /// Instantiates every feature and lets it contribute services to the container.
    /// </summary>
    public void DiscoverAndRegister(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (Type featureType in _typeRegistry.GetTypesImplementing<IPangeaFeature>()
                     .Where(type => type is { IsAbstract: false, IsInterface: false })
                     .OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            if (_features.Any(feature => feature.GetType() == featureType)) continue;

            IPangeaFeature instance = Create(featureType);
            instance.ConfigureServices(services);
            _features.Add(instance);
        }
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
