using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;

namespace CdCSharp.Pangea.Files;

public enum FileStatus
{
    Pending,
    Working,
    Done,
    Skipped,
    Failed,
    Canceled
}

/// <summary>A file in a list being worked through, and how it went.</summary>
/// <remarks>Its properties are bound to, so they are set on the UI thread: <see cref="FileBatch"/> does.</remarks>
public sealed partial class FileItem : ObservableModel
{
    private readonly FileListTexts _texts;

    /// <param name="path">The file.</param>
    /// <param name="texts">What <see cref="StatusText"/> says; English when null.</param>
    public FileItem(string path, FileListTexts? texts = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Path = path;
        _texts = texts ?? FileListTexts.English;

        try
        {
            Length = new FileInfo(path).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public string Path { get; }

    public string Name => System.IO.Path.GetFileName(Path);

    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";

    /// <summary>The size when the item was made, in bytes; 0 when it could not be read.</summary>
    public long Length { get; }

    /// <summary>What the list shows under the name: «1920 × 1080», «3:07 · mp3 · 44.1 kHz».</summary>
    [Binding] private string _description = "";

    /// <summary>What was read to describe it - an image's header, a probe of a video - for whoever processes it next.</summary>
    [Binding] private object? _details;

    [Binding] private FileStatus _status;

    /// <summary>What the result came to, shown when done: «1.2 MB (-40%)».</summary>
    [Binding] private string? _result;

    [Binding] private string? _error;

    [Binding] private string? _outputPath;

    /// <summary>From 0 to 1 while working; 0 when there is nothing to say how far.</summary>
    [Binding] private double _progress;

    public string SizeText => FileSize.Text(Length);

    /// <summary>The description and the size: what the list shows under the name.</summary>
    public string Detail => string.IsNullOrEmpty(Description) ? SizeText : $"{Description} · {SizeText}";

    public bool IsWorking => Status == FileStatus.Working;

    public bool IsDone => Status == FileStatus.Done;

    public bool HasFailed => Status == FileStatus.Failed;

    public string StatusText => Status switch
    {
        FileStatus.Working => Progress > 0 ? Progress.ToString("P0", System.Globalization.CultureInfo.CurrentCulture) : _texts.Working,
        FileStatus.Done => Result ?? _texts.Done,
        FileStatus.Skipped => Result ?? _texts.Skipped,
        FileStatus.Failed => _texts.Failed,
        FileStatus.Canceled => _texts.Canceled,
        _ => ""
    };

    /// <summary>Back to pending, forgetting the last run's result.</summary>
    public void Reset()
    {
        Status = FileStatus.Pending;
        Result = null;
        Error = null;
        OutputPath = null;
        Progress = 0;
    }
}

/// <summary>What describing a file came to: the text the list shows, and what was read to say it.</summary>
public sealed record FileDescription(string Text, object? Details = null);

/// <summary>Sizes as a person reads them.</summary>
public static class FileSize
{
    /// <summary>«512 B», «3.4 KB», «12.25 MB», «1.5 GB», in the current culture.</summary>
    public static string Text(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024):0.##} MB",
        _ => $"{bytes / (1024d * 1024 * 1024):0.##} GB"
    };
}
