using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Tests;

/// <summary>
/// Feature discovery and configuration.
/// </summary>
/// <remarks>
/// These tests run against a real <see cref="TypeRegistry"/>, so the toolkit's own features are
/// discovered alongside the fixtures below. Assertions are therefore written as "contains" rather
/// than "equals": the fixtures are what is under test, the rest is legitimate company.
/// </remarks>
public class FeatureRegistryTests
{
    /// <summary>Marker service so a fixture can prove its ConfigureServices ran.</summary>
    public sealed class ProbeService;

    public sealed class ProbeFeature : IPangeaFeature
    {
        public string Name => "Probe";
        public Version Version => new(1, 0, 0);

        public void ConfigureServices(IServiceCollection services) => services.AddSingleton<ProbeService>();
    }

    /// <summary>Only fails when its application-level configuration is actually invoked.</summary>
    public sealed class FailingFeature : IPangeaFeature
    {
        public const string FailureMessage = "configuration exploded";

        public string Name => "Failing";
        public Version Version => new(1, 0, 0);

        public void ConfigureServices(IServiceCollection services) { }

        public void ConfigureApplication(IServiceProvider serviceProvider, IPangeaApplicationContext applicationContext) =>
            throw new InvalidOperationException(FailureMessage);
    }

    private static FeatureRegistry Discover(IServiceCollection services)
    {
        FeatureRegistry registry = new(new TypeRegistry());
        registry.DiscoverAndRegister(services);
        return registry;
    }

    [Fact]
    public void DiscoversFeaturesImplementingTheInterface()
    {
        FeatureRegistry registry = Discover(new ServiceCollection());

        Assert.Contains(registry.Features, feature => feature is ProbeFeature);
    }

    [Fact]
    public void LetsEachFeatureRegisterItsServices()
    {
        ServiceCollection services = new();

        Discover(services);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ProbeService));
    }

    [Fact]
    public void DoesNotRegisterTheSameFeatureTwice()
    {
        FeatureRegistry registry = new(new TypeRegistry());

        registry.DiscoverAndRegister(new ServiceCollection());
        registry.DiscoverAndRegister(new ServiceCollection());

        Assert.Single(registry.Features, feature => feature is ProbeFeature);
    }

    [Fact]
    public void SkipsAbstractionsThatCannotBeInstantiated()
    {
        FeatureRegistry registry = Discover(new ServiceCollection());

        Assert.DoesNotContain(registry.Features, feature => feature.GetType().IsAbstract);
    }

    [Fact]
    public void TwoRegistries_DoNotShareDiscoveredFeatures()
    {
        FeatureRegistry first = Discover(new ServiceCollection());
        FeatureRegistry second = new(new TypeRegistry());

        Assert.NotEmpty(first.Features);
        Assert.Empty(second.Features);
    }

    [Fact]
    public void AFeatureThatFailsToConfigure_AbortsStartupAndNamesItself()
    {
        FeatureRegistry registry = Discover(new ServiceCollection());
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => registry.ConfigureApplication(provider, new StubApplicationContext()));

        // Half a configured feature is worse than none: a swallowed failure here surfaces much
        // later as a missing service.
        Assert.Contains("Failing", error.Message);
        Assert.Contains(nameof(FailingFeature), error.Message);
        Assert.Equal(FailingFeature.FailureMessage, error.InnerException?.Message);
    }

    [Fact]
    public void RejectsMissingArguments()
    {
        FeatureRegistry registry = new(new TypeRegistry());
        ServiceProvider provider = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<ArgumentNullException>(() => new FeatureRegistry(null!));
        Assert.Throws<ArgumentNullException>(() => registry.DiscoverAndRegister(null!));
        Assert.Throws<ArgumentNullException>(() => registry.ConfigureApplication(null!, new StubApplicationContext()));
        Assert.Throws<ArgumentNullException>(() => registry.ConfigureApplication(provider, null!));
    }

    private sealed class StubApplicationContext : IPangeaApplicationContext
    {
        public void AddStyle(object style) { }

        public void RemoveStyle(object style) { }

        public bool HasStyle<T>() where T : class => false;

        public T? GetRequiredService<T>() where T : class => null;

        public object? GetApplication() => null;
    }
}
