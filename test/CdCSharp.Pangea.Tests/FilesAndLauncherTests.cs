using Avalonia.Headless.XUnit;
using Avalonia.Platform.Storage;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Tests;

/// <summary>
/// Pickers and the launcher answer "nothing" rather than throwing when there is nothing to show them
/// with, or nothing to open: a button that does nothing is reported, not crashed on.
/// </summary>
public class FilesAndLauncherTests
{
    private sealed class StubApplication : PangeaApplication;

    /// <summary>Started without a lifetime, as a headless session or a designer is: no main window, no single view.</summary>
    private static IServiceProvider WithoutAWindow() => PangeaExtensions.ConfigureServices(new StubApplication());

    [AvaloniaFact]
    public async Task Without_a_window_to_own_them_pickers_answer_that_nothing_was_chosen()
    {
        IFilePickerService pickers = WithoutAWindow().GetRequiredService<IFilePickerService>();

        Assert.Empty(await pickers.OpenFilesAsync("Add files"));
        Assert.Null(await pickers.OpenFileAsync("Open", [new FileType("Images", ["png"])], Path.GetTempPath()));
        Assert.Null(await pickers.OpenFolderAsync("Destination"));
        Assert.Null(await pickers.SaveFileAsync("Save", "result.png"));
    }

    [AvaloniaFact]
    public async Task Nothing_is_opened_or_shown_for_a_path_that_does_not_exist()
    {
        ISystemLauncher launcher = WithoutAWindow().GetRequiredService<ISystemLauncher>();
        string missing = Path.Combine(Path.GetTempPath(), "pangea-" + Guid.NewGuid().ToString("N"), "nothing.png");

        Assert.False(await launcher.OpenAsync(missing));
        Assert.False(await launcher.ShowInFolderAsync(missing));
        Assert.False(await launcher.OpenAsync(""));
    }

    [AvaloniaFact]
    public void Extensions_are_understood_however_they_are_written()
    {
        FilePickerFileType images = FilePickerService.ToAvalonia(new FileType("Images", ["png", ".JPG", "*.webp", " "]));

        Assert.Equal("Images", images.Name);
        Assert.Equal(["*.png", "*.jpg", "*.webp"], images.Patterns);
    }

    [AvaloniaFact]
    public void Any_file_is_offered_as_the_system_writes_it()
    {
        FilePickerFileType all = FilePickerService.ToAvalonia(FileType.All);

        Assert.True(FileType.All.IsAnyFile);
        Assert.Equal(["*.*"], all.Patterns);
        Assert.Empty(FileType.All.PlainExtensions);
    }
}
