using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Storage.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Storage.Tests;

/// <summary>
/// The feature is what picks a path provider, and a feature that throws aborts startup. Neither had
/// a test.
/// </summary>
public class StorageFeatureTests
{
    private static ServiceProvider Configure(StorageOptions? options = null)
    {
        ServiceCollection services = [];
        new StorageFeature().ConfigureServices(services);

        if (options is not null)
        {
            services.AddSingleton(Options.Create(options));
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public void RegistersAPathProviderAndTheStorageService()
    {
        ServiceProvider services = Configure();

        Assert.NotNull(services.GetService<IPlatformPathProvider>());
        Assert.NotNull(services.GetService<IStorageService>());
    }

    [Fact]
    public void PortableMode_WinsOverTheOperatingSystem()
    {
        ServiceProvider services = Configure(new StorageOptions { UsePortableMode = true });

        Assert.IsType<PortablePlatformPathProvider>(services.GetService<IPlatformPathProvider>());
    }

    [Fact]
    public void WithoutPortableMode_TheProviderMatchesTheHostOperatingSystem()
    {
        ServiceProvider services = Configure(new StorageOptions { UsePortableMode = false });
        IPlatformPathProvider provider = services.GetRequiredService<IPlatformPathProvider>();

        Type expected =
            OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() ? typeof(MobilePlatformPathProvider) :
            OperatingSystem.IsWindows() ? typeof(WindowsPlatformPathProvider) :
            OperatingSystem.IsLinux() ? typeof(LinuxPlatformPathProvider) :
            OperatingSystem.IsMacOS() ? typeof(MacOSPlatformPathProvider) :
            typeof(PortablePlatformPathProvider);

        Assert.IsType(expected, provider);
    }

    [Fact]
    public void ThePathProviderIsASingleton()
    {
        ServiceProvider services = Configure();

        Assert.Same(services.GetService<IPlatformPathProvider>(), services.GetService<IPlatformPathProvider>());
    }

    [Fact]
    public void TheFeatureIdentifiesItself()
    {
        StorageFeature feature = new();

        Assert.Equal("Storage", feature.Name);
        Assert.NotNull(feature.Version);
    }
}
