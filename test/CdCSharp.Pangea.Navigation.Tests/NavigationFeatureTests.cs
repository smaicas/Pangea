using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Navigation.Abstractions;
using CdCSharp.Pangea.Navigation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Navigation.Tests;

/// <summary>
/// Declaring the destination in the request type makes "does this request reach anything?" a
/// question with an answer, so it is asked once at startup rather than discovered as a screen that
/// silently loads nothing.
/// </summary>
public class NavigationFeatureTests
{
    /// <summary>Points at a view model that never accepts it.</summary>
    private sealed record Undeliverable(int Value) : INavigationRequest<PlainViewModel>;

    [Fact]
    public void RequestsThatReachTheirDestination_Pass() =>
        NavigationFeature.VerifyRequestsReachTheirDestination([typeof(ShowOrder), typeof(ShowReport)]);

    [Fact]
    public void ARequestNobodyAccepts_AbortsStartupNamingBothSides()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            NavigationFeature.VerifyRequestsReachTheirDestination([typeof(Undeliverable)]));

        Assert.Contains(nameof(Undeliverable), error.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(PlainViewModel), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFeatureRegistersWhatTheHostNeeds()
    {
        ServiceCollection services = [];
        new NavigationFeature().ConfigureServices(services);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IViewLocator));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(INavigationService));
    }

    [Fact]
    public void TheFeatureIdentifiesItself()
    {
        NavigationFeature feature = new();

        Assert.Equal("Navigation", feature.Name);
        Assert.NotNull(feature.Version);
    }
}
