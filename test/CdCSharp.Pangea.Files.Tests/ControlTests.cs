using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CdCSharp.Pangea.Dialogs;

namespace CdCSharp.Pangea.Files.Tests;

/// <summary>The panel and the path box, in a running application, with the application's pickers.</summary>
public sealed class ControlTests : IDisposable
{
    private readonly Workspace _files = new();

    public void Dispose() => _files.Dispose();

    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    [AvaloniaFact]
    public void The_panel_speaks_the_lists_words_and_counts_its_files()
    {
        FileList list = new() { Texts = FileListTexts.English with { AddFiles = "Añadir archivos", ManyFiles = "{0} archivos", Empty = "Arrastra aquí" } };
        FileListPanel panel = new() { Files = list };
        Window window = new() { Content = panel };
        window.Show();

        Assert.Equal("Arrastra aquí", Find<TextBlock>(panel, "EmptyLabel").Text);
        Assert.True(Find<StackPanel>(panel, "Empty").IsVisible);
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "Añadir archivos");

        list.Add([_files.File("a.png"), _files.File("b.png")]);

        Assert.Equal("2 archivos", Find<TextBlock>(panel, "Count").Text);
        Assert.False(Find<StackPanel>(panel, "Empty").IsVisible);

        panel.EmptyText = "Arrastra aquí imágenes";
        Assert.Equal("Arrastra aquí imágenes", Find<TextBlock>(panel, "EmptyLabel").Text);

        window.Close();
    }

    [AvaloniaFact]
    public async Task Adding_files_asks_the_applications_picker_with_the_lists_types()
    {
        string a = _files.File("a.png");
        FileType images = new("Imágenes", ["png"]);
        FileList list = new() { Types = [images, FileType.All] };
        FileListPanel panel = new() { Files = list };
        Window window = new() { Content = panel };
        window.Show();

        FilesTestApp.Pickers.Choosing(a);
        Find<Button>(panel, "AddFiles").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => list.Items.Count == 1);

        Assert.Equal(a, list.Items[0].Path);
        Assert.Equal(images, FilesTestApp.Pickers.LastRequest?.Types[0]);

        window.Close();
    }

    [AvaloniaFact]
    public async Task The_path_box_starts_where_its_path_points_and_takes_what_is_chosen()
    {
        string current = _files.File("in/old.png");
        string chosen = _files.File("in/new.png");
        FilePickerBox box = new() { Path = current, Extensions = ["png"], FilterName = "PNG", Title = "Elige una imagen" };
        Window window = new() { Content = box };
        window.Show();

        FilesTestApp.Pickers.Choosing(chosen);
        box.GetVisualDescendants().OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => box.Path == chosen);

        Assert.Equal("Elige una imagen", FilesTestApp.Pickers.LastRequest?.Title);
        Assert.Equal(current, FilesTestApp.Pickers.LastRequest?.StartIn);
        Assert.Equal(["PNG", "All"], FilesTestApp.Pickers.LastRequest?.Types.Select(type => type.Name));

        window.Close();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime until = DateTime.UtcNow.AddSeconds(10);

        while (!condition() && DateTime.UtcNow < until)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition(), "Timed out.");
    }
}
