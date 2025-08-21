using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Localization.Abstractions;
using CdCSharp.Pangea.Localization.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Localization;

[PangeaFeature(typeof(LocalizationFeature))]
public class LocalizationFeature : IPangeaFeature
{
    public string Name => "Localization";
    public Version Version => new(1, 0, 0);

    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<LocalizationOptions>(options => 
        {
        });

        services.AddSingleton<ILocalizationService, LocalizationService>();
    }
}