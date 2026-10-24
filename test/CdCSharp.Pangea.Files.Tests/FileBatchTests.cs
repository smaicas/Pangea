using CdCSharp.Pangea.Testing.Dispatchers;

namespace CdCSharp.Pangea.Files.Tests;

/// <summary>
/// A batch goes through every file, a failure costs only its file, cancelling stops it cleanly, and
/// nothing a file claimed is left behind when it does not come to a result.
/// </summary>
public sealed class FileBatchTests : IDisposable
{
    private readonly Workspace _files = new();

    public void Dispose() => _files.Dispose();

    private FileList List(int count, int bytes = 100)
    {
        FileList list = new();
        list.Add(Enumerable.Range(1, count).Select(index => _files.File($"in/{index}.png", bytes)));
        return list;
    }

    [Fact]
    public async Task Every_file_ends_with_its_result_and_the_counts_add_up()
    {
        FileList list = List(4);
        string output = Path.Combine(_files.Root, "out");
        FileBatch batch = new(new InlineUIDispatcher());
        int changes = 0;
        batch.Changed += (_, _) => Interlocked.Increment(ref changes);

        await batch.RunAsync(list.Items, async work =>
        {
            await Task.Yield();

            if (work.Position == 2) return FileOutcome.Skipped();

            string path = work.Resolve("{name}-small", output, ".png", ConflictPolicy.Rename)!;
            await File.WriteAllBytesAsync(path, new byte[40], work.CancellationToken);
            return FileOutcome.Written(path, "40 B");
        }, parallelism: 3);

        Assert.Equal(3, batch.Done);
        Assert.Equal(1, batch.Skipped);
        Assert.Equal(0, batch.Failed);
        Assert.Equal(300, batch.BytesBefore);
        Assert.Equal(120, batch.BytesAfter);
        Assert.Equal(output, batch.LastFolder);
        Assert.False(batch.IsRunning);

        Assert.Equal(FileStatus.Skipped, list.Items[1].Status);
        Assert.All(list.Items.Where((_, index) => index != 1), item =>
        {
            Assert.Equal(FileStatus.Done, item.Status);
            Assert.Equal("40 B", item.StatusText);
            Assert.True(File.Exists(item.OutputPath));
        });

        // Started, four files, ended.
        Assert.Equal(6, changes);
    }

    [Fact]
    public async Task A_failure_is_its_files_and_leaves_no_placeholder_behind()
    {
        FileList list = List(3);
        string output = Path.Combine(_files.Root, "out");
        FileBatch batch = new(new InlineUIDispatcher()) { DescribeError = ex => "No: " + ex.Message };

        await batch.RunAsync(list.Items, work =>
        {
            string path = work.Resolve(null, output, ".png", ConflictPolicy.Rename)!;

            if (work.Position == 2) throw new InvalidDataException("not an image");

            return Task.FromResult(FileOutcome.Written(path));
        });

        Assert.Equal(2, batch.Done);
        Assert.Equal(1, batch.Failed);
        Assert.Equal(FileStatus.Failed, list.Items[1].Status);
        Assert.Equal("No: not an image", list.Items[1].Error);
        Assert.Equal(["1.png", "3.png"], Directory.GetFiles(output).Select(Path.GetFileName).Order());
    }

    [Fact]
    public async Task A_claim_the_outcome_does_not_name_is_deleted()
    {
        FileList list = List(1);
        string output = Path.Combine(_files.Root, "out");
        FileBatch batch = new(new InlineUIDispatcher());

        await batch.RunAsync(list.Items, work =>
        {
            string draft = work.Claim(output, "draft", ".tmp", ConflictPolicy.Rename)!;
            string final = work.Claim(output, "final", ".png", ConflictPolicy.Rename)!;
            File.WriteAllText(draft, "half");
            return Task.FromResult(FileOutcome.Written(final));
        });

        Assert.Equal(["final.png"], Directory.GetFiles(output).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Cancelling_stops_the_files_running_marks_the_rest_and_deletes_their_claims()
    {
        FileList list = List(5);
        string output = Path.Combine(_files.Root, "out");
        FileBatch batch = new(new InlineUIDispatcher());
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task run = batch.RunAsync(list.Items, async work =>
        {
            work.Resolve(null, output, ".png", ConflictPolicy.Rename);
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, work.CancellationToken);
            return FileOutcome.Done();
        }, parallelism: 2);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.True(batch.IsRunning);

        batch.Cancel();
        await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(batch.IsRunning);
        Assert.All(list.Items, item => Assert.Equal(FileStatus.Canceled, item.Status));
        Assert.Empty(Directory.GetFiles(output));
    }

    [Fact]
    public async Task A_second_run_while_one_goes_on_does_nothing()
    {
        FileList list = List(1);
        FileBatch batch = new(new InlineUIDispatcher());
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int processed = 0;

        Task first = batch.RunAsync(list.Items, async _ =>
        {
            Interlocked.Increment(ref processed);
            await release.Task;
            return FileOutcome.Done();
        });

        await batch.RunAsync(list.Items, _ =>
        {
            Interlocked.Increment(ref processed);
            return Task.FromResult(FileOutcome.Done());
        });

        release.SetResult();
        await first;

        Assert.Equal(1, processed);
    }

    [Fact]
    public async Task Progress_reaches_the_item_while_it_works()
    {
        FileList list = List(1);
        FileBatch batch = new(new InlineUIDispatcher());
        double seen = 0;

        await batch.RunAsync(list.Items, work =>
        {
            work.Progress.Report(0.25);
            seen = work.Item.Progress;
            return Task.FromResult(FileOutcome.Done());
        });

        Assert.Equal(0.25, seen);
        Assert.Equal(0, list.Items[0].Progress);
        Assert.Equal(FileStatus.Done, list.Items[0].Status);
    }
}
