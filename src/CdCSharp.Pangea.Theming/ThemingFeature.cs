using Avalonia.Styling;
using Avalonia.Threading;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Theming.Abstractions;
using CdCSharp.Pangea.Theming.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Theming;

[PangeaFeature(typeof(ThemingFeature))]
public class ThemingFeature : IPangeaFeature
{
    public string Name => "Theming";
    public Version Version => new(1, 0, 0);

    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<ThemingOptions>(options => 
        {
        });

        services.AddSingleton<IThemeService, ThemeService>();
    }

    public void ConfigureApplication(IServiceProvider serviceProvider, IPangeaApplicationContext applicationContext)
    {
        try
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                bool alreadyExists = applicationContext.HasStyle<PangeaUI>();

                if (!alreadyExists)
                {
                    PangeaUI pangeaUI = new PangeaUI();
                    applicationContext.AddStyle(pangeaUI);
                    
                    System.Diagnostics.Debug.WriteLine("🎨 PangeaUI auto-inyectado por ThemingFeature");
                }
                
                IOptions<ThemingOptions> themingOptions = serviceProvider.GetRequiredService<IOptions<ThemingOptions>>();
                IThemeService themeService = serviceProvider.GetRequiredService<IThemeService>();
                
                foreach (KeyValuePair<string, string> theme in themingOptions.Value.CustomThemes)
                    themeService.RegisterTheme(theme.Key, theme.Value);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ConfigureApplication error: {ex.Message}");
        }
    }
}