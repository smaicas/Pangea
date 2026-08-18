using Avalonia.Markup.Xaml;
using CdCSharp.Pangea;
using CdCSharp.Pangea.Data;
using CdCSharp.Pangea.Data.Configuration;
using CdCSharp.Pangea.Data.Sqlite;
using CdCSharp.Pangea.Storage;
using CdCSharp.Pangea.Theming;
using CdCSharp.Pangea.Theming.Palettes;
using Microsoft.Extensions.DependencyInjection;
using PangeaDataApp.Data;
using PangeaDataApp.Themes;

namespace PangeaDataApp;

public partial class App : PangeaApplication
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void Configure(IServiceCollection services)
    {
        // Each feature reads its own options. A feature the application never configures keeps the
        // defaults it registered for itself.
        services.Configure<ThemingOptions>(options =>
            options.Themes[PangeaTheme.DefaultName] = new PangeaTheme(new AppLightPalette(), new AppDarkPalette()));

        // The name of the folder the database goes in, per platform: %APPDATA%\PangeaDataApp on
        // Windows, ~/.config/PangeaDataApp on Linux, ~/Library/Application Support on macOS.
        services.Configure<StorageOptions>(options => options.ApplicationName = "PangeaDataApp");

        services.AddPangeaDbContext<AppDbContext>(db =>
        {
            db.UseSqlite("notes.db");

            // The default, spelled out because it is the decision worth understanding: pending
            // migrations are applied at startup, on the user's machine, with a copy of the database
            // taken first and put back if the migration fails.
            db.Options.Migration = MigrationStrategy.MigrateWithBackup;
            db.Options.BackupsToKeep = 3;
        });

        // Runs at startup, after the schema is up to date, on every run.
        services.AddDataSeeder<AppDbContext, WelcomeNoteSeeder>();
    }
}
