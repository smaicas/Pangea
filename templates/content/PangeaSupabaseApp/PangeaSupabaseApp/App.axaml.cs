using Avalonia.Markup.Xaml;
using CdCSharp.Pangea;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Storage;
using CdCSharp.Pangea.Supabase;
using CdCSharp.Pangea.Theming;
using CdCSharp.Pangea.Theming.Palettes;
using Microsoft.Extensions.DependencyInjection;
using PangeaSupabaseApp.Data;
using PangeaSupabaseApp.Services;
using PangeaSupabaseApp.Themes;
using PangeaSupabaseApp.ViewModels;
using PangeaSupabaseApp.Views;

namespace PangeaSupabaseApp;

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
        services.AddSingleton<NotesCache>();
        services.AddSingleton<INotesBackend, NotesBackend>();
        services.AddSingleton<NotesRepository>();
        services.AddSingleton<IPangeaAsyncInitializer, StartupInitializer>();

        // ThemeMetrics.Touch sizes every control for a thumb instead of a pointer: nothing tappable
        // below 48, and the type a step up to match. Sizes come from the theme, so a phone needs
        // this argument rather than a style override per control - including the controls nobody
        // remembers to style, which is where a 32-high combo box on a phone comes from.
        services.Configure<ThemingOptions>(options =>
            options.Themes[PangeaTheme.DefaultName] =
                new PangeaTheme(new AppLightPalette(), new AppDarkPalette(), ThemeMetrics.Touch));

        services.Configure<StorageOptions>(options => options.ApplicationName = "PangeaSupabaseApp");

        services.Configure<SupabaseOptions>(options =>
        {
            // The base project URL, not the REST endpoint: the client appends /rest/v1 itself.
            //
            // Both of these are public by design: the key identifies the project, not the user, and
            // what a request may read or write is decided by row level security against the
            // signed-in account. The service_role key is the opposite of that and must never appear
            // here, or anywhere else a device can read it.
            options.Url = "SUPABASE_URL";
            options.AnonKey = "SUPABASE_ANON_KEY";

            // An account before the user has been asked for anything, so the application is usable
            // seconds after it is installed. Requires anonymous sign-ins to be enabled on the
            // project.
            options.SignInAnonymouslyOnStart = true;

            // The application opens on what the device already knows. A backend that is not there is
            // a stale screen and a quiet note, not a screen that refuses to load.
            options.RequireConnectionAtStartup = false;
        });
    }
}
