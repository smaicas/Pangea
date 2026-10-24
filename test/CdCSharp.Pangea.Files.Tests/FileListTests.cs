namespace CdCSharp.Pangea.Files.Tests;

/// <summary>What a list takes in, how it keeps its order, and what it says about each file.</summary>
public sealed class FileListTests : IDisposable
{
    private readonly Workspace _files = new();

    public void Dispose() => _files.Dispose();

    private static bool IsImage(string path) => Path.GetExtension(path) is ".png" or ".jpg";

    [Fact]
    public void A_folder_brings_every_accepted_file_inside_it_in_order_and_nothing_twice()
    {
        string b = _files.File("photos/b.png");
        string a = _files.File("photos/a.jpg");
        string deep = _files.File("photos/trip/c.png");
        _files.File("photos/notes.txt");

        FileList list = new(IsImage);
        int changes = 0;
        list.Changed += (_, _) => changes++;

        Assert.Equal(3, list.Add([Path.Combine(_files.Root, "photos")]));
        Assert.Equal(0, list.Add([a, Path.Combine(_files.Root, "photos")]));

        Assert.Equal([a, b, deep], list.Paths);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void One_file_is_one_entry_however_its_path_is_written()
    {
        string path = _files.File("photos/a.png");
        string folder = Path.GetDirectoryName(path)!;

        FileList list = new();

        Assert.Equal(1, list.Add([path, Path.Combine(folder, ".", "a.png"), Path.Combine(folder, "..", "photos", "a.png")]));
        Assert.Equal(path, list.Items[0].Path);
    }

    [Fact]
    public void Without_recursion_a_folder_brings_only_its_own_files()
    {
        string top = _files.File("photos/a.png");
        _files.File("photos/trip/b.png");

        FileList list = new(IsImage);
        list.Add([Path.Combine(_files.Root, "photos")], recursive: false);

        Assert.Equal([top], list.Paths);
    }

    [Fact]
    public void A_file_the_list_does_not_accept_and_one_that_is_not_there_are_left_out()
    {
        FileList list = new(IsImage);

        Assert.Equal(0, list.Add([_files.File("song.mp3"), Path.Combine(_files.Root, "missing.png"), ""]));
        Assert.Empty(list.Items);
    }

    [Fact]
    public void Moving_sorting_and_removing_keep_the_list_and_say_so()
    {
        FileList list = new();
        list.Add([_files.File("c.png"), _files.File("a.png"), _files.File("b.png")]);
        int changes = 0;
        list.Changed += (_, _) => changes++;

        list.Sort();
        Assert.Equal(["a.png", "b.png", "c.png"], list.Items.Select(item => item.Name));

        list.Move(list.Items[0], 1);
        list.Move(list.Items[0], -1);
        Assert.Equal(["b.png", "a.png", "c.png"], list.Items.Select(item => item.Name));

        list.Remove(list.Items[1]);
        list.Clear();
        list.Clear();

        Assert.Empty(list.Items);
        Assert.Equal(4, changes);
    }

    [Fact]
    public async Task Each_file_is_described_as_it_is_added()
    {
        TaskCompletionSource read = new(TaskCreationOptions.RunContinuationsAsynchronously);

        FileList list = new(describe: async (path, _) =>
        {
            await read.Task;
            return new FileDescription("640 × 480", Details: Path.GetFileName(path));
        });

        string path = _files.File("a.png", bytes: 2048);
        list.Add([path]);
        FileItem item = list.Items[0];

        Assert.Equal("2 KB", item.Detail);

        read.SetResult();
        await WaitUntilAsync(() => item.Description.Length > 0);

        Assert.Equal("640 × 480 · 2 KB", item.Detail);
        Assert.Equal("a.png", item.Details);
    }

    [Fact]
    public void A_file_that_cannot_be_described_is_still_listed()
    {
        FileList list = new(describe: (_, _) => throw new InvalidDataException("not an image"));

        list.Add([_files.File("broken.png")]);

        Assert.Single(list.Items);
        Assert.Equal("", list.Items[0].Description);
    }

    [Fact]
    public void An_item_says_how_it_went_in_the_lists_words()
    {
        FileListTexts spanish = FileListTexts.English with { Working = "Procesando…", Done = "Hecho", Failed = "Error", ManyFiles = "{0} archivos" };
        FileItem item = new(_files.File("a.png"), spanish);

        item.Status = FileStatus.Working;
        Assert.Equal("Procesando…", item.StatusText);
        Assert.True(item.IsWorking);

        item.Progress = 0.5;
        Assert.Equal((0.5).ToString("P0", System.Globalization.CultureInfo.CurrentCulture), item.StatusText);

        item.Status = FileStatus.Done;
        Assert.Equal("Hecho", item.StatusText);

        item.Result = "1 KB (-50%)";
        Assert.Equal("1 KB (-50%)", item.StatusText);

        item.Reset();
        Assert.Equal(FileStatus.Pending, item.Status);
        Assert.Null(item.Result);
        Assert.Equal("", item.StatusText);
        Assert.Equal("3 archivos", spanish.Count(3));
        Assert.Equal("", spanish.Count(0));
    }

    [Fact]
    public void Sizes_read_as_a_person_reads_them()
    {
        Assert.Equal("512 B", FileSize.Text(512));
        Assert.Equal($"{1.5:0.#} KB", FileSize.Text(1536));
        Assert.Equal($"{2.0:0.##} MB", FileSize.Text(2L * 1024 * 1024));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (!condition()) await Task.Delay(10, timeout.Token);
    }
}
