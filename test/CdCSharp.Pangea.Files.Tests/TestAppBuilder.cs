using Avalonia;
using Avalonia.Headless;
using CdCSharp.Pangea;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Files.Tests;
using CdCSharp.Pangea.Platform;
using CdCSharp.Pangea.Testing.Fakes;
using CdCSharp.Pangea.Theming;
using Microsoft.Extensions.DependencyInjection;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace CdCSharp.Pangea.Files.Tests;

/// <summary>A Pangea application, headless, whose pickers and launcher answer from the tests.</summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<FilesTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .UsePangea();
}

public sealed class FilesTestApp : PangeaApplication
{
    public static RecordingFilePickerService Pickers { get; } = new();

    public static RecordingSystemLauncher Launcher { get; } = new();

    public override void Initialize() => Styles.Add(new PangeaStyles());

    public override void Configure(IServiceCollection services)
    {
        services.AddSingleton<IFilePickerService>(Pickers);
        services.AddSingleton<ISystemLauncher>(Launcher);
    }
}
