using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using CdCSharp.Pangea;
using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace PangeaMobileApp.Android;

/// <summary>
/// Where the Android head configures Avalonia.
/// </summary>
/// <remarks>
/// <para>
/// On the <c>Application</c>, not the activity. Avalonia 12 moved it: <c>AvaloniaMainActivity</c>
/// stopped being generic and lost <c>CustomizeAppBuilder</c>, and
/// <see cref="AvaloniaAndroidApplication{TApp}"/> took both over. The activity is now only where
/// the view is shown; this is where the application is built.
/// </para>
/// <para>
/// <c>UsePangea</c> is the call that matters: it builds the container, discovers the toolkit's
/// features and registers the view models. Without it the shell comes up with no data context and
/// every binding is empty.
/// </para>
/// </remarks>
[Application(Label = "PangeaMobileApp", Icon = "@drawable/icon", SupportsRtl = true)]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    /// <summary>
    /// The constructor Android itself calls.
    /// </summary>
    /// <remarks>
    /// The runtime creates this from Java, handing over a handle rather than calling a parameterless
    /// constructor. Leaving it out compiles and then fails at launch with nothing to read.
    /// </remarks>
    public MainApplication(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership) { }

    /// <summary>
    /// Registers what only Android can build, and then starts the application.
    /// </summary>
    /// <remarks>
    /// The toolkit's own secret store and connectivity check work everywhere and are least accurate
    /// here: a file with an encrypted blob, and the network interfaces. These reach the Keystore and
    /// the ConnectivityManager. UsePangea takes them because this is the only code that is both
    /// inside the head and part of starting the application - the shared library cannot see Android,
    /// and the toolkit's own defaults are registered with TryAdd so what is registered here wins.
    /// </remarks>
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UsePangea(services =>
            {
                services.AddSingleton<ISecretStore>(_ => new KeystoreSecretStore(this));
                services.AddSingleton<IConnectivity>(_ => new AndroidConnectivity(this));
            });
}
