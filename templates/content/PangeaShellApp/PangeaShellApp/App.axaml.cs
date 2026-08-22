using Avalonia.Markup.Xaml;
using CdCSharp.Pangea;
using CdCSharp.Pangea.Localization;
using CdCSharp.Pangea.Storage;
using CdCSharp.Pangea.Theming;
using CdCSharp.Pangea.Theming.Palettes;
using Microsoft.Extensions.DependencyInjection;
using PangeaShellApp.Services;
using PangeaShellApp.Themes;

namespace PangeaShellApp;

public partial class App : PangeaApplication
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void Configure(IServiceCollection services)
    {
        // View models deriving from ViewModelBase are registered automatically. Everything else
        // that a view model asks for is registered here.
        services.AddSingleton<AppSettingsStore>();

        // Each feature reads its own options. A feature the application never configures keeps the
        // defaults it registered for itself.
        services.Configure<ThemingOptions>(options =>
            options.Themes[PangeaTheme.DefaultName] = new PangeaTheme(new AppLightPalette(), new AppDarkPalette()));

        services.Configure<StorageOptions>(options => options.ApplicationName = "PangeaShellApp");

        services.Configure<LocalizationOptions>(options =>
        {
            // Where the strings live. Nothing is found without this.
            options.ResourceAssemblies.Add(typeof(App).Assembly);
            options.SupportedCultures = ["en-US", "es-ES"];
            options.DefaultCulture = "en-US";
        });
    }
}
