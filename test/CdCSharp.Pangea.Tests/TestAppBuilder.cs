using Avalonia;
using Avalonia.Headless;
using CdCSharp.Pangea.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace CdCSharp.Pangea.Tests;

/// <summary>
/// A bare Avalonia application with no windowing backend, enough for the window manager to create,
/// show and close real windows.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<WindowTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class WindowTestApp : Application;
