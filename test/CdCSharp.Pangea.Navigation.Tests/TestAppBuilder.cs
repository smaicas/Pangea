using Avalonia;
using Avalonia.Headless;
using CdCSharp.Pangea.Navigation.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace CdCSharp.Pangea.Navigation.Tests;

/// <summary>
/// A bare Avalonia application with no windowing backend, enough for the navigation host to be
/// attached to a real visual tree.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<NavigationTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class NavigationTestApp : Application;
