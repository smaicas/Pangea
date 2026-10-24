using Avalonia.Platform.Storage;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Windows;

namespace CdCSharp.Pangea.Dialogs;

/// <inheritdoc cref="IFilePickerService"/>
/// <remarks>
/// The picker belongs to the main window on desktop and to the single view elsewhere, both looked up
/// when a picker opens rather than when this is built: a test container or a designer builds every
/// screen without having either.
/// </remarks>
public sealed class FilePickerService(IServiceProvider services, IUIDispatcher dispatcher) : IFilePickerService
{
    public async Task<IReadOnlyList<string>> OpenFilesAsync(string title, IReadOnlyList<FileType>? types = null, string? startIn = null) =>
        await OpenAsync(title, multiple: true, types, startIn);

    public async Task<string?> OpenFileAsync(string title, IReadOnlyList<FileType>? types = null, string? startIn = null) =>
        (await OpenAsync(title, multiple: false, types, startIn)).FirstOrDefault();

    public Task<string?> OpenFolderAsync(string title, string? startIn = null) => dispatcher.InvokeAsync(async () =>
    {
        if (StorageProvider() is not { } storage) return null;

        IReadOnlyList<IStorageFolder> folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await StartFolderAsync(storage, startIn)
        });

        return folders.Select(folder => folder.TryGetLocalPath()).FirstOrDefault(path => path is not null);
    });

    public Task<string?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileType>? types = null, string? startIn = null) =>
        dispatcher.InvokeAsync(async () =>
        {
            if (StorageProvider() is not { } storage) return null;

            IReadOnlyList<FileType> offered = types is { Count: > 0 } ? types : [FileType.All];

            IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggestedName,
                FileTypeChoices = offered.Select(ToAvalonia).ToArray(),
                DefaultExtension = offered[0].IsAnyFile ? null : offered[0].PlainExtensions.FirstOrDefault(),
                ShowOverwritePrompt = true,
                SuggestedStartLocation = await StartFolderAsync(storage, startIn)
            });

            return file?.TryGetLocalPath();
        });

    private Task<IReadOnlyList<string>> OpenAsync(string title, bool multiple, IReadOnlyList<FileType>? types, string? startIn) =>
        dispatcher.InvokeAsync<IReadOnlyList<string>>(async () =>
        {
            if (StorageProvider() is not { } storage) return [];

            IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = multiple,
                FileTypeFilter = (types is { Count: > 0 } ? types : [FileType.All]).Select(ToAvalonia).ToArray(),
                SuggestedStartLocation = await StartFolderAsync(storage, startIn)
            });

            return files.Select(file => file.TryGetLocalPath()).OfType<string>().ToArray();
        });

    /// <summary>The main window's, or the single view's, or none yet.</summary>
    private IStorageProvider? StorageProvider() => MainTopLevel.Find(services)?.StorageProvider;

    private static async Task<IStorageFolder?> StartFolderAsync(IStorageProvider storage, string? startIn)
    {
        if (string.IsNullOrWhiteSpace(startIn)) return null;

        string? folder = Directory.Exists(startIn) ? startIn : Path.GetDirectoryName(startIn);

        return folder is not null && Directory.Exists(folder) ? await storage.TryGetFolderFromPathAsync(folder) : null;
    }

    internal static FilePickerFileType ToAvalonia(FileType type) => type.IsAnyFile
        ? new FilePickerFileType(type.Name) { Patterns = ["*.*"], MimeTypes = ["*/*"] }
        : new FilePickerFileType(type.Name) { Patterns = type.PlainExtensions.Select(extension => "*." + extension).ToArray() };
}
