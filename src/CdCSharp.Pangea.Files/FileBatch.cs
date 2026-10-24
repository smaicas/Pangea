using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CdCSharp.Pangea.Files;

/// <summary>What processing one file came to.</summary>
public sealed record FileOutcome
{
    private FileOutcome(string? outputPath, string? summary, bool skipped)
    {
        OutputPath = outputPath;
        Summary = summary;
        IsSkipped = skipped;
    }

    /// <summary>The result written; null when there is none to show, as for a skipped file.</summary>
    public string? OutputPath { get; }

    /// <summary>What the list says for it: «1.2 MB (-40%)». The texts' Done or Skipped when null.</summary>
    public string? Summary { get; }

    public bool IsSkipped { get; }

    /// <summary>Done, with the result written to <paramref name="outputPath"/>.</summary>
    public static FileOutcome Written(string outputPath, string? summary = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        return new FileOutcome(outputPath, summary, skipped: false);
    }

    /// <summary>Done without writing anything: there was nothing to do.</summary>
    public static FileOutcome Done(string? summary = null) => new(null, summary, skipped: false);

    /// <summary>Left alone, because its result was already there.</summary>
    public static FileOutcome Skipped(string? summary = null) => new(null, summary, skipped: true);
}

/// <summary>One file's turn in a batch: what to process, and the means to report and to name.</summary>
public sealed class FileWork
{
    private readonly List<string> _claimed = [];

    internal FileWork(FileItem item, int position, IProgress<double> progress, CancellationToken cancellationToken)
    {
        Item = item;
        Position = position;
        Progress = progress;
        CancellationToken = cancellationToken;
    }

    public FileItem Item { get; }

    /// <summary>Where the file is in the batch, from 1: what <c>{n}</c> in a name template is.</summary>
    public int Position { get; }

    /// <summary>From 0 to 1; shown on the file as it goes.</summary>
    public IProgress<double> Progress { get; }

    /// <summary>Cancelled when the batch is.</summary>
    public CancellationToken CancellationToken { get; }

    internal IReadOnlyList<string> ClaimedPaths
    {
        get
        {
            lock (_claimed) return _claimed.ToArray();
        }
    }

    /// <summary>
    /// <see cref="OutputNames.Claim"/>, remembered: if this file fails or the batch is cancelled,
    /// what was claimed for it is deleted, and so is a claim the outcome does not name.
    /// </summary>
    public string? Claim(string folder, string name, string extension, ConflictPolicy policy)
    {
        string? path = OutputNames.Claim(folder, name, extension, policy);

        // An overwrite takes the existing file, which is not this batch's to delete.
        if (path is not null && policy != ConflictPolicy.Overwrite)
        {
            lock (_claimed) _claimed.Add(path);
        }

        return path;
    }

    /// <summary><see cref="OutputNames.Resolve"/>, remembered as <see cref="Claim"/> is.</summary>
    public string? Resolve(string? template, string folder, string extension, ConflictPolicy policy,
        int width = 0, int height = 0, string? format = null, string fallback = "file") =>
        Claim(folder, OutputNames.Apply(template, Item.Path, Position, width, height, format, fallback), extension, policy);
}

/// <summary>
/// Runs a list of files through one piece of work, a few at a time, and keeps the list and the
/// counts up to date as it goes.
/// </summary>
/// <remarks>
/// <para>
/// Each file goes Working, then Done, Skipped, Failed or Canceled, with its result or its error, set
/// on the UI thread. A failure is that file's: the rest carry on. Cancelling stops the ones running
/// and leaves the rest Canceled. What a file claimed through <see cref="FileWork.Claim"/> is
/// deleted when it does not come to a result, so a failed run leaves no empty files behind.
/// </para>
/// <code>
/// await batch.RunAsync(files.Items, async work =>
/// {
///     string? output = work.Resolve(template, folder, ".png", ConflictPolicy.Rename);
///     if (output is null) return FileOutcome.Skipped();
///
///     await converter.ConvertAsync(work.Item.Path, output, work.Progress, work.CancellationToken);
///     return FileOutcome.Written(output);
/// }, parallelism: 2);
/// </code>
/// </remarks>
public sealed class FileBatch
{
    private readonly IUIDispatcher _ui;
    private readonly ILogger _logger;
    private CancellationTokenSource? _cancellation;
    private int _done;
    private int _skipped;
    private int _failed;
    private long _bytesBefore;
    private long _bytesAfter;
    private string? _lastFolder;

    /// <param name="ui">Where the list's items are updated.</param>
    /// <param name="logger">Where a failure is logged with its exception; nowhere when null.</param>
    public FileBatch(IUIDispatcher ui, ILogger? logger = null)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// The message a failed file shows. The exception's own by default; an application that throws
    /// its own exceptions for the person can add the type for anything else.
    /// </summary>
    public Func<Exception, string> DescribeError { get; init; } = exception => exception.Message;

    public bool IsRunning => Volatile.Read(ref _cancellation) is not null;

    // Counted atomically: the workers report from several threads at once wherever the dispatcher runs inline.
    public int Done => Volatile.Read(ref _done);

    public int Skipped => Volatile.Read(ref _skipped);

    public int Failed => Volatile.Read(ref _failed);

    /// <summary>The sizes of the files done, before and after: for «12 MB → 4 MB».</summary>
    public long BytesBefore => Interlocked.Read(ref _bytesBefore);

    public long BytesAfter => Interlocked.Read(ref _bytesAfter);

    /// <summary>The folder of the first result written in the last run, or null.</summary>
    public string? LastFolder => Volatile.Read(ref _lastFolder);

    /// <summary>Raised on the UI thread as a run starts, as each file ends, and as the run ends.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Processes <paramref name="items"/>, <paramref name="parallelism"/> at a time. Does nothing
    /// while a run is going on, or for no items.
    /// </summary>
    /// <param name="items">The files, in order; taken as they are when the run starts.</param>
    /// <param name="process">The work for one file. Throws to fail it; is cancelled through <see cref="FileWork.CancellationToken"/>.</param>
    /// <param name="parallelism">How many files at once; at least one.</param>
    /// <returns>When every file has ended, or the run was cancelled.</returns>
    public async Task RunAsync(IEnumerable<FileItem> items, Func<FileWork, Task<FileOutcome>> process, int parallelism = 1)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(process);

        FileItem[] run = items.ToArray();
        if (run.Length == 0) return;

        using CancellationTokenSource cancellation = new();
        if (Interlocked.CompareExchange(ref _cancellation, cancellation, null) is not null) return;

        _done = _skipped = _failed = 0;
        _bytesBefore = _bytesAfter = 0;
        _lastFolder = null;

        await _ui.InvokeAsync(() =>
        {
            foreach (FileItem item in run) item.Reset();
            Changed?.Invoke(this, EventArgs.Empty);
        });

        try
        {
            await Parallel.ForEachAsync(
                run.Select((item, index) => (item, position: index + 1)),
                new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallelism), CancellationToken = cancellation.Token },
                async (entry, token) => await ProcessAsync(entry.item, entry.position, process, token));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            Volatile.Write(ref _cancellation, null);

            await _ui.InvokeAsync(() =>
            {
                foreach (FileItem item in run.Where(item => item.Status is FileStatus.Pending or FileStatus.Working)) item.Status = FileStatus.Canceled;
                Changed?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    /// <summary>Stops the run: the files running are cancelled, the rest are not started.</summary>
    public void Cancel()
    {
        try
        {
            Volatile.Read(ref _cancellation)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The run ended between the read and the cancel.
        }
    }

    private async Task ProcessAsync(FileItem item, int position, Func<FileWork, Task<FileOutcome>> process, CancellationToken token)
    {
        await _ui.InvokeAsync(() => item.Status = FileStatus.Working);

        FileWork work = new(item, position, new UIProgress(_ui, item), token);
        FileOutcome outcome;

        try
        {
            outcome = await process(work);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            DeleteClaims(work, keep: null);
            await _ui.InvokeAsync(() => item.Status = FileStatus.Canceled);
            throw;
        }
        catch (Exception ex)
        {
            DeleteClaims(work, keep: null);
            _logger.LogWarning(ex, "Processing {File} failed", item.Path);

            string message = DescribeError(ex);
            Interlocked.Increment(ref _failed);

            await _ui.InvokeAsync(() =>
            {
                item.Error = message;
                item.Status = FileStatus.Failed;
                Changed?.Invoke(this, EventArgs.Empty);
            });
            return;
        }

        DeleteClaims(work, keep: outcome.OutputPath);

        long length = outcome.OutputPath is { } output && File.Exists(output) ? new FileInfo(output).Length : 0;

        if (outcome.IsSkipped)
        {
            Interlocked.Increment(ref _skipped);
        }
        else
        {
            Interlocked.Increment(ref _done);

            if (outcome.OutputPath is { } written)
            {
                Interlocked.Add(ref _bytesBefore, item.Length);
                Interlocked.Add(ref _bytesAfter, length);
                Interlocked.CompareExchange(ref _lastFolder, Path.GetDirectoryName(written), null);
            }
        }

        await _ui.InvokeAsync(() =>
        {
            item.OutputPath = outcome.OutputPath;
            item.Result = outcome.Summary;
            item.Progress = 0;
            item.Status = outcome.IsSkipped ? FileStatus.Skipped : FileStatus.Done;
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>What a file claimed and did not come to: placeholders and half-written results.</summary>
    private void DeleteClaims(FileWork work, string? keep)
    {
        foreach (string path in work.ClaimedPaths)
        {
            if (keep is not null && string.Equals(Path.GetFullPath(path), Path.GetFullPath(keep), StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogInformation(ex, "Could not delete {File}, left behind by {Source}", path, work.Item.Path);
            }
        }
    }

    /// <summary>Progress reported from any thread, shown on the item from the UI thread.</summary>
    private sealed class UIProgress(IUIDispatcher ui, FileItem item) : IProgress<double>
    {
        public void Report(double value) => ui.Post(() =>
        {
            if (item.Status == FileStatus.Working) item.Progress = Math.Clamp(value, 0, 1);
        });
    }
}
