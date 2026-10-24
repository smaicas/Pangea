using CdCSharp.Pangea.Dialogs;

namespace CdCSharp.Pangea.Testing.Fakes;

/// <summary>Which picker was asked for.</summary>
public enum FilePickerKind
{
    OpenFiles,
    OpenFile,
    OpenFolder,
    SaveFile
}

/// <summary>A picker that was asked for, and how.</summary>
/// <param name="Kind">Which one.</param>
/// <param name="Title">Its title.</param>
/// <param name="Types">The kinds of file it offered; empty for a folder, or when the caller offered any file.</param>
/// <param name="StartIn">Where it was asked to start.</param>
/// <param name="SuggestedName">The name a save picker started with; null for the others.</param>
public sealed record FilePickerRequest(
    FilePickerKind Kind,
    string Title,
    IReadOnlyList<FileType> Types,
    string? StartIn,
    string? SuggestedName);

/// <summary>
/// Answers pickers from a script instead of showing them, and remembers what was asked.
/// </summary>
/// <remarks>
/// A picker nothing was scripted for is cancelled - the person closed it - which is the answer a
/// screen most often gets wrong, and the one a test gets without arranging anything.
/// </remarks>
public sealed class RecordingFilePickerService : IFilePickerService
{
    private readonly Queue<IReadOnlyList<string>> _scripted = new();

    /// <summary>Every picker asked for, in order.</summary>
    public List<FilePickerRequest> Requests { get; } = [];

    /// <summary>The last picker asked for, or <see langword="null"/>.</summary>
    public FilePickerRequest? LastRequest => Requests.Count > 0 ? Requests[^1] : null;

    /// <summary>
    /// Queues what the person chooses in the next picker: files, a folder, or where to save. A
    /// picker for one item takes the first. No paths at all is a cancel.
    /// </summary>
    public RecordingFilePickerService Choosing(params string[] paths)
    {
        _scripted.Enqueue(paths);
        return this;
    }

    /// <summary>Queues a cancel: the next picker is closed without choosing.</summary>
    public RecordingFilePickerService Cancelling() => Choosing();

    public Task<IReadOnlyList<string>> OpenFilesAsync(string title, IReadOnlyList<FileType>? types = null, string? startIn = null) =>
        Task.FromResult(Answer(new FilePickerRequest(FilePickerKind.OpenFiles, title, types ?? [], startIn, null)));

    public Task<string?> OpenFileAsync(string title, IReadOnlyList<FileType>? types = null, string? startIn = null) =>
        Task.FromResult(Answer(new FilePickerRequest(FilePickerKind.OpenFile, title, types ?? [], startIn, null)).FirstOrDefault());

    public Task<string?> OpenFolderAsync(string title, string? startIn = null) =>
        Task.FromResult(Answer(new FilePickerRequest(FilePickerKind.OpenFolder, title, [], startIn, null)).FirstOrDefault());

    public Task<string?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileType>? types = null, string? startIn = null) =>
        Task.FromResult(Answer(new FilePickerRequest(FilePickerKind.SaveFile, title, types ?? [], startIn, suggestedName)).FirstOrDefault());

    private IReadOnlyList<string> Answer(FilePickerRequest request)
    {
        Requests.Add(request);
        return _scripted.Count > 0 ? _scripted.Dequeue() : [];
    }
}
