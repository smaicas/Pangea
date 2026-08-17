using Avalonia;
using Avalonia.Headless;
using CdCSharp.Pangea.Theming;
using CdCSharp.Pangea.Theming.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace CdCSharp.Pangea.Theming.Tests;

/// <summary>
/// Boots Avalonia with no windowing backend, applying the toolkit theme exactly the way
/// <see cref="ThemingFeature"/> does at runtime: PangeaUI added to Application.Styles.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<ThemeTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class ThemeTestApp : Application
{
    public override void Initialize() => Styles.Add(new PangeaUI());
}
