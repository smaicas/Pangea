using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Localization.Abstractions;
using CdCSharp.Pangea.Localization.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Localization;

public class LocalizationFeature : IPangeaFeature
{
    public string Name => "Localization";
    public Version Version => new(1, 0, 0);

    public void ConfigureServices(IServiceCollection services)
    {
        // Defaults only; the application overrides them with its own services.Configure call.
        services.Configure<LocalizationOptions>(_ => { });

        services.AddSingleton<ILocalizationService, LocalizationService>();
    }
}