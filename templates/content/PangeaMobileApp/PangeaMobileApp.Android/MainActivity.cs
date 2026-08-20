using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace PangeaMobileApp.Android;

/// <summary>
/// The Android head: the launcher activity, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// Empty on purpose. Avalonia 12 moved the application's configuration onto the Application class -
/// see <see cref="MainApplication"/> - and left the activity as the place the view is put on
/// screen.
/// </para>
/// <para>
/// <c>ConfigChanges</c> lists what the activity handles itself. Without them Android destroys and
/// recreates it on a rotation or a keyboard, which for an Avalonia application means throwing the
/// whole UI away and rebuilding it mid-gesture.
/// </para>
/// </remarks>
[Activity(
    Label = "PangeaMobileApp",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation
                           | ConfigChanges.ScreenSize
                           | ConfigChanges.UiMode
                           | ConfigChanges.Keyboard
                           | ConfigChanges.KeyboardHidden)]
public class MainActivity : AvaloniaMainActivity
{
}
