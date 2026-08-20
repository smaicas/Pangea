# Android and iOS

What changes when the application has no windows.

## The one rule

**A `Window` cannot be constructed on Android, iOS or in the browser.** No windowing platform is
registered there, so the constructor throws. Everything below follows from that.

The lifetime hands the application one `Control` and never a second. Pangea puts a
`PangeaShellHost` there - the application's view at the bottom, an overlay layer above it - so the
splash and every dialog have somewhere to go without a window being opened for them.

## The shell

An application with mobile heads needs a `MainView`, not just a `MainWindow`:

<!-- not-compiled: InitializeComponent is written by the XAML compiler, which needs the .axaml -->
```csharp
using Avalonia.Controls;

// Views/MainView.axaml.cs - a UserControl, found by name the way MainWindow is.
public partial class MainView : UserControl
{
    public MainView() => InitializeComponent();
}
```

| What | Found as |
|---|---|
| The shell control | `MainView`, or `PangeaOptions.Window.MainViewType` |
| Its view model | `MainViewModel`, then `MainWindowViewModel`, or `Window.MainViewModelType` |

Falling back to `MainWindowViewModel` is deliberate: one shell view model serves both heads. The
desktop head keeps its `MainWindow`, whose content is usually the same `MainView`.

Nothing else changes. Navigation, `NavigationHost`, theming, localization, storage and view models
all behave as they do on desktop.

## Dialogs and splash

`IDialogService` works unchanged. What it opens differs: a modal window on desktop, a card in the
overlay layer on a phone - modal because the layer takes the pointer input the UI underneath would
have got. Escape answers it as a cancel, the way dismissing the window does.

The splash is the same story, and `PangeaStartupOptions.SplashWindowType` is the one place a desktop
application's configuration does not carry over: a `Window` cannot be shown, so the built-in splash
view stands in and a warning says so. Point it at a `UserControl` to use your own on both.

`IShellPresenter` is where this decision lives, should an application need to ask:

```csharp
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Shell;

public partial class ShellAwareViewModel : ViewModelBase
{
    private readonly IShellPresenter _shell;

    public ShellAwareViewModel(IServiceProvider services, IShellPresenter shell) : base(services) =>
        _shell = shell;

    public bool OnAPhone => _shell.IsSingleView;
}
```

## Storage

`IStorageService` resolves the platform's own directories - `files/` on Android, `Library/` on iOS -
and the application sandbox is what keeps them private. `GetCachePath()` on iOS is `Library/Caches`,
which the system may reclaim: nothing that cannot be rebuilt belongs there.

Portable mode is not available on a device. An installed bundle is read only, so asking for it gets
the platform directories anyway.

## Project layout

One shared library, one head per platform:

```
MyApp/           # net10.0 library: App, view models, views, MainView
MyApp.Desktop/   # net10.0, WinExe
MyApp.Android/   # net10.0-android
MyApp.iOS/       # net10.0-ios
```

The heads hold only the entry point, and Avalonia 12 puts that entry point in a different place on
each platform:

| Platform | Where `UsePangea()` goes |
|---|---|
| Android | `[Application] class MyApplication : AvaloniaAndroidApplication<App>`, overriding `CustomizeAppBuilder` |
| iOS | `[Register] class AppDelegate : AvaloniaAppDelegate<App>`, overriding `CustomizeAppBuilder` |
| Desktop | `AppBuilder.Configure<App>()` in `Main` |

**Android moved in Avalonia 12.** `AvaloniaMainActivity` is no longer generic and no longer has
`CustomizeAppBuilder`; the activity is now only where the view is shown, and it is written empty.
Configuring it there instead produces `CS0115: no suitable method found to override`, which names
what you guessed and never what was there.

<!-- not-compiled: the Android SDK is not referenced by the project that compiles these samples -->
```csharp
[Application(Label = "MyApp", Icon = "@drawable/icon")]
public class MyApplication : AvaloniaAndroidApplication<App>
{
    // Android creates this from Java, handing over a handle. Leaving the constructor out compiles
    // and then fails at launch with nothing to read.
    public MyApplication(IntPtr handle, JniHandleOwnership ownership) : base(handle, ownership) { }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).WithInterFont().UsePangea();
}

[Activity(Label = "MyApp", MainLauncher = true, ConfigurationChanges = ConfigChanges.Orientation
    | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.Keyboard
    | ConfigChanges.KeyboardHidden)]
public class MainActivity : AvaloniaMainActivity;
```

Leave the `<application>` node out of `AndroidManifest.xml`: `[Application]` writes it, and the
class has to be the one Android instantiates or the AppBuilder is never customised.

**The activity's theme has to be an AppCompat one.** `AvaloniaActivity` derives from
`AppCompatActivity`, which refuses to start under anything else:

> `java.lang.IllegalStateException: You need to use a Theme.AppCompat theme (or descendant) with
> this activity`

thrown from `OnCreate`, before a pixel is drawn. `@android:style/Theme.Material.*` is the tempting
wrong answer. Use `Theme.AppCompat.DayNight.NoActionBar` so the system bars follow the phone's own
light/dark setting, and put the colours in `Resources/values/styles.xml` with the dark ones in
`Resources/values-night/styles.xml` under the same style name.

```xml
<style name="MyTheme.NoActionBar" parent="Theme.AppCompat.DayNight.NoActionBar">
    <item name="android:statusBarColor">#F9FAFB</item>
    <item name="android:navigationBarColor">#F9FAFB</item>
    <item name="android:windowLightStatusBar">true</item>
    <item name="android:windowBackground">#F9FAFB</item>
</style>
```

The build catches a missing parent - aapt2 resolves it - so a theme that compiles is a theme that
exists. What it cannot catch is a theme that exists and is the wrong family, which is this one.

## What only a head can provide

The toolkit registers an `ISecretStore` and an `IConnectivity` that work everywhere, and both are at
their least accurate on a phone: a file holding an encrypted blob, and a reading of the network
interfaces. A head that can reach the Android Keystore or the iOS Keychain replaces them.

**Where the registration goes matters.** A head cannot use `App.Configure` - that lives in the
shared library, which by definition cannot see Android - and a feature in the head is found only if
the head is scanned or carries a generated catalog. What a head always has is the `AppBuilder` it
builds, so that is where the hook is:

<!-- not-compiled: the Android SDK is not referenced by the project that compiles these samples -->
```csharp
protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
    base.CustomizeAppBuilder(builder)
        .WithInterFont()
        .UsePangea(services =>
        {
            services.AddSingleton<ISecretStore>(_ => new KeystoreSecretStore(this));
            services.AddSingleton<IConnectivity>(_ => new AndroidConnectivity(this));
        });
```

These run before everything else, and the toolkit's own services are registered with `TryAdd`, so
what the platform provides is what the application resolves and the defaults fill in the rest. The
application's `Configure` still runs last and still has the final word - which is what an
application means when it registers a service knowing full well it is on a phone.

Nothing in the ordinary build compiles a platform head: they need the Android and iOS SDKs, which
arrive with a workload. `test/CdCSharp.Pangea.Templates.Compile.Mobile` does, and is opt-in for that
reason - `dotnet build test/CdCSharp.Pangea.Templates.Compile.Mobile` with the workloads installed,
and one CI job on a Mac. **Run it after touching a head.** A Keychain overload that no longer
existed once sat in the template for weeks, because the only thing that reads that code is an
application generated from it.

Both templates ship working implementations of all four - `KeystoreSecretStore`,
`AndroidConnectivity`, `KeychainSecretStore` and `PathMonitorConnectivity` - so the fastest way to
write them is to generate a project and read them.

Three decisions in those worth keeping if you write your own:

- **The Keychain item is `AfterFirstUnlock`, not `WhenUnlocked`.** An application refreshing a token
  in the background reads it while the phone is in a pocket, and the stricter accessibility fails
  those reads and signs the user out for no reason they can see.
- **The Keychain item is not synchronised to iCloud.** A session belongs to the device that signed
  in; restoring it onto a second phone puts two devices on one refresh token, which a server is
  entitled to treat as theft.
- **Both connectivity implementations report only transitions.** Android raises its callbacks
  generously and `NWPathMonitor` reports every change to the path, including ones that do not alter
  whether there is a network at all - and an outbox waiting on `WaitForConnectionAsync` would drain
  on every one of them.

`ISecretStore.Protection` is worth reading rather than assuming: it answers `Device` behind the
Keystore and the Keychain, and `OperatingSystem` or `UserOnly` on a desktop. An application deciding
whether to offer "stay signed in", or how long a session may live, has a real answer to work from.

## Pitfalls

- **A view sized for a desktop window** is the commonest one. A phone is narrow and its keyboard
  takes half the screen: make the content region scroll, and let the shell fill.
- **`IWindowManager` does nothing here.** `GetMainWindow()` returns null, and it is not a fault.
- **A splash configured as a `Window`** starts the application with the built-in one instead.
- **EF Core on iOS** needs the interpreter enabled, and is worth avoiding for a mobile head that
  could use plain files or a remote backend instead.
- **A platform service registered by a feature loses.** Features register before the toolkit's
  own defaults; only `App.Configure` runs after them.
