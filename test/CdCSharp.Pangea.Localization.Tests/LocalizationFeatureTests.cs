using CdCSharp.Pangea.Localization.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Localization.Tests;

/// <summary>
/// The feature registers what the rest of the application resolves, and a feature that throws
/// aborts startup. It had no test.
/// </summary>
public class LocalizationFeatureTests
{
    private static ServiceProvider Configure(Action<IServiceCollection>? application = null)
    {
        ServiceCollection services = [];
        new LocalizationFeature().ConfigureServices(services);
        application?.Invoke(services);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void RegistersTheLocalizationService()
    {
        ServiceProvider services = Configure();

        Assert.NotNull(services.GetService<ILocalizationService>());
    }

    [Fact]
    public void TheServiceIsASingleton()
    {
        ServiceProvider services = Configure();

        Assert.Same(services.GetService<ILocalizationService>(), services.GetService<ILocalizationService>());
    }

    [Fact]
    public void OptionsAreResolvableWithoutTheApplicationConfiguringAnything()
    {
        ServiceProvider services = Configure();

        LocalizationOptions options = services.GetRequiredService<IOptions<LocalizationOptions>>().Value;

        Assert.False(string.IsNullOrWhiteSpace(options.DefaultCulture));
        Assert.NotEmpty(options.SupportedCultures);
    }

    /// <summary>
    /// The feature only supplies defaults, so whatever the application configures has to win.
    /// </summary>
    [Fact]
    public void TheApplicationsOwnConfigurationWins()
    {
        ServiceProvider services = Configure(collection =>
            collection.Configure<LocalizationOptions>(options => options.DefaultCulture = "eu-ES"));

        Assert.Equal("eu-ES", services.GetRequiredService<IOptions<LocalizationOptions>>().Value.DefaultCulture);
    }

    [Fact]
    public void TheFeatureIdentifiesItself()
    {
        LocalizationFeature feature = new();

        Assert.Equal("Localization", feature.Name);
        Assert.NotNull(feature.Version);
    }
}
