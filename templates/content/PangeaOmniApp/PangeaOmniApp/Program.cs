using Avalonia;
using CdCSharp.Pangea;

namespace PangeaOmniApp;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // UsePangea builds the container and discovers the toolkit's features - CdCSharp.Pangea.Apps
    // among them, which connects to the host when one is running.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UsePangea();
}
