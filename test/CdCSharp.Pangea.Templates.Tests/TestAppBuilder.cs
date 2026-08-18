using Avalonia;
using Avalonia.Headless;
using CdCSharp.Pangea;
using CdCSharp.Pangea.Templates.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace CdCSharp.Pangea.Templates.Tests;

/// <summary>
/// Starts the shell template's own application, headless.
/// </summary>
/// <remarks>
/// <para>
/// Not a stand-in: this is <c>PangeaShellApp.App</c> with the wiring the template ships, so a
/// service the template forgets to register or a screen the view locator cannot find fails here
/// rather than on someone's first run of a generated project.
/// </para>
/// <para>
/// A headless session has no application lifetime and will not accept one, so there is no desktop
/// window here. Everything else runs: the container is built, the features configure the
/// application, and the shell navigates to its first screen.
/// </para>
/// </remarks>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<PangeaShellApp.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .UsePangea();
}
