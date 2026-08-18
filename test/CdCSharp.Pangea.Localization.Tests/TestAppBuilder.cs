using Avalonia;
using Avalonia.Headless;
using CdCSharp.Pangea.Localization.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace CdCSharp.Pangea.Localization.Tests;

/// <summary>
/// A bare Avalonia application with no windowing backend, enough for the language selector to be
/// built and bound.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<LocalizationTestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class LocalizationTestApp : Application;
