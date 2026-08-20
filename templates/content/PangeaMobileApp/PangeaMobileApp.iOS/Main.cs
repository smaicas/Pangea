using UIKit;

namespace PangeaMobileApp.iOS;

/// <summary>Hands the process to UIKit, which builds <see cref="AppDelegate"/>.</summary>
public static class Application
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
