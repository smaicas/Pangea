using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using CdCSharp.Pangea.Windows;

namespace CdCSharp.Pangea.Tests;

/// <summary>
/// Escape closing a window, which is opt-in and stays that way.
/// </summary>
/// <remarks>
/// A modal dialog dismissed by Escape is a convention everywhere. An ordinary window closed by
/// Escape is not, and on one holding unsaved work it would destroy it with a keystroke - so the
/// toolkit does not do it unless asked.
/// </remarks>
public class WindowBehaviorTests
{
    private static Window Show(bool closeOnEscape)
    {
        Window window = new() { Width = 300, Height = 200, Content = new Button { Content = "ok" } };

        if (closeOnEscape) WindowBehavior.SetCloseOnEscape(window, true);

        window.Show();
        window.Activate();
        Pump();

        return window;
    }

    private static void Pump()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }
    }

    [AvaloniaFact]
    public void ByDefault_EscapeLeavesTheWindowAlone()
    {
        Window window = Show(closeOnEscape: false);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();

        Assert.True(window.IsVisible, "Escape closed a window that never asked for it.");
        window.Close();
    }

    [AvaloniaFact]
    public void WhenAsked_EscapeCloses()
    {
        Window window = Show(closeOnEscape: true);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();

        Assert.False(window.IsVisible, "Escape did not close a window that asked for it.");
    }

    [AvaloniaFact]
    public void OtherKeysAreNotAWayOut()
    {
        Window window = Show(closeOnEscape: true);

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();

        Assert.True(window.IsVisible);
        window.Close();
    }

    /// <summary>Turning it back off has to actually detach the behaviour.</summary>
    [AvaloniaFact]
    public void TurningItOffAgain_RestoresTheDefault()
    {
        Window window = Show(closeOnEscape: true);

        WindowBehavior.SetCloseOnEscape(window, false);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();

        Assert.True(window.IsVisible);
        window.Close();
    }
}
