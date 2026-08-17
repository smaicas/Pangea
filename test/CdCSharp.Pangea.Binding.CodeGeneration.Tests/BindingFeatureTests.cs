using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// The binding feature does its work at compile time, so it registers nothing. That is a decision
/// worth pinning: a feature is discovered and invoked at startup whether or not it has anything to
/// do, and one that quietly started registering services would change what every application gets.
/// </summary>
public class BindingFeatureTests
{
    [Fact]
    public void RegistersNothing()
    {
        ServiceCollection services = [];

        new BindingFeature().ConfigureServices(services);

        Assert.Empty(services);
    }

    /// <summary>
    /// A feature that throws here aborts startup, so taking the default implementation - rather
    /// than writing one - is load-bearing.
    /// </summary>
    [Fact]
    public void ConfiguringTheApplicationDoesNothingAndThrowsNothing()
    {
        IPangeaFeature feature = new BindingFeature();

        feature.ConfigureApplication(new EmptyServices(), null!);
    }

    private sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    public void TheFeatureIdentifiesItself()
    {
        BindingFeature feature = new();

        Assert.Equal("Binding", feature.Name);
        Assert.NotNull(feature.Version);
    }

    [Fact]
    public void TheFeatureIsDiscoverableAsOne() =>
        Assert.IsAssignableFrom<IPangeaFeature>(new BindingFeature());
}
