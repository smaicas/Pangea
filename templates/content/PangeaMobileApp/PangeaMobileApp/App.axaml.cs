using Avalonia.Markup.Xaml;
using CdCSharp.Pangea;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Storage;
using CdCSharp.Pangea.Theming;
using CdCSharp.Pangea.Theming.Palettes;
using Microsoft.Extensions.DependencyInjection;
using PangeaMobileApp.Themes;
using PangeaMobileApp.ViewModels;
using PangeaMobileApp.Views;

namespace PangeaMobileApp;

public partial class App : PangeaApplication
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Names the shell explicitly for both kinds of platform.
    /// </summary>
    /// <remarks>
    /// Discovery would find these anyway. Naming them means a rename cannot quietly turn into an
    /// application that starts with an empty window, which is the failure that takes longest to
    /// recognise.
    /// </remarks>
    public override PangeaOptions ConfigurePangeaOptions(PangeaOptions options)
    {
        options.Window.MainWindowType = typeof(MainWindow);
        options.Window.MainViewType = typeof(MainView);
        options.Window.MainViewModelType = typeof(MainViewModel);

        return options;
    }

    public override void Configure(IServiceCollection services)
    {
        // View models deriving from ViewModelBase are registered automatically. Everything else
        // that a view model asks for is registered here.

        // Each feature reads its own options. A feature the application never configures keeps the
        // defaults it registered for itself.
        // ThemeMetrics.Touch sizes every control for a thumb instead of a pointer: nothing tappable
        // below 48, and the type a step up to match. Sizes come from the theme, so a phone needs
        // this argument rather than a style override per control - including the controls nobody
        // remembers to style, which is where a 32-high combo box on a phone comes from.
        services.Configure<ThemingOptions>(options =>
            options.Themes[PangeaTheme.DefaultName] =
                new PangeaTheme(new AppLightPalette(), new AppDarkPalette(), ThemeMetrics.Touch));

        services.Configure<StorageOptions>(options => options.ApplicationName = "PangeaMobileApp");
    }
}
