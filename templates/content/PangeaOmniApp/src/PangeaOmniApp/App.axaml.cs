using Avalonia.Markup.Xaml;
using CdCSharp.Pangea;
using CdCSharp.Pangea.Storage;
using CdCSharp.Pangea.Theming;
using CdCSharp.Pangea.Theming.Palettes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PangeaOmniApp.Services;
using PangeaOmniApp.Themes;

namespace PangeaOmniApp;

public partial class App : PangeaApplication
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void Configure(IServiceCollection services)
    {
        services.AddLogging(logging => logging.AddDebug().SetMinimumLevel(LogLevel.Information));

        // The data folder keeps the application's name, so it is the same however it is started:
        // by the host, from an IDE, or on its own.
        services.Configure<StorageOptions>(options => options.ApplicationName = "PangeaOmniApp");

        // Its own look when it runs alone; inside the host, the host's palette replaces it.
        services.Configure<ThemingOptions>(options =>
            options.Themes[PangeaTheme.DefaultName] = new PangeaTheme(new PangeaOmniAppLightPalette(), new PangeaOmniAppDarkPalette()));

        services.AddSingleton<GreetingLog>();

        // Nothing to register for the bus: the request handlers are found by the generator, and
        // IAppBus and IAppHostContext come with CdCSharp.Pangea.Apps.
    }
}
