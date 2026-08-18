using Avalonia.Styling;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Localization.Abstractions;
using CdCSharp.Pangea.Theming.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PangeaShellApp.Services;

/// <summary>
/// Restores the culture and appearance the user last chose.
/// </summary>
/// <remarks>
/// A <see cref="IPangeaFeature"/> is the toolkit's unit of extension: any class implementing it is
/// found at startup, gets to register services, and is then handed the running application. This
/// one registers nothing and only needs the second half.
/// <para>
/// The work is started rather than awaited. <see cref="ConfigureApplication"/> runs on the UI
/// thread before the main window is shown, and blocking it on a file read would delay the window
/// for no benefit: the defaults are already applied, and the saved values replace them a moment
/// later.
/// </para>
/// </remarks>
public sealed class AppStartupFeature : IPangeaFeature
{
    public string Name => "AppStartup";

    public Version Version => new(1, 0, 0);

    public void ConfigureServices(IServiceCollection services)
    {
        // Nothing to register: AppSettingsStore is registered by App.Configure, alongside the
        // rest of the application's own services.
    }

    public void ConfigureApplication(IServiceProvider serviceProvider, IPangeaApplicationContext applicationContext) =>
        _ = RestoreAsync(serviceProvider);

    private static async Task RestoreAsync(IServiceProvider serviceProvider)
    {
        ILogger logger = serviceProvider.GetRequiredService<ILogger<AppStartupFeature>>();

        try
        {
            AppSettings settings = await serviceProvider.GetRequiredService<AppSettingsStore>().LoadAsync();

            ILocalizationService localization = serviceProvider.GetRequiredService<ILocalizationService>();
            IThemeService theming = serviceProvider.GetRequiredService<IThemeService>();
            IUIDispatcher dispatcher = serviceProvider.GetRequiredService<IUIDispatcher>();

            await dispatcher.InvokeAsync(() =>
            {
                // A culture that is no longer offered is not an error worth failing over: it is a
                // settings file written by an older build.
                if (localization.SupportedCultures.Any(culture =>
                        string.Equals(culture.Name, settings.Culture, StringComparison.OrdinalIgnoreCase)))
                {
                    localization.SetCulture(settings.Culture);
                }

                theming.SetVariant(settings.IsDark ? ThemeVariant.Dark : ThemeVariant.Light);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saved settings could not be restored; the defaults stay in place.");
        }
    }
}
