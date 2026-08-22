using Avalonia;
using CdCSharp.Pangea;

namespace PangeaDataApp;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // UsePangea builds the container, discovers the toolkit's features and registers view models.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UsePangea();
}
