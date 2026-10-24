using CdCSharp.Pangea.Dialogs;
using System.Collections.ObjectModel;

namespace CdCSharp.Pangea.Files;

/// <summary>
/// The files a screen works on: added by picker or by dropping files and folders - every file inside
/// a folder that the list accepts - without repeats, in an order the person can change.
/// </summary>
/// <remarks>
/// Changed on the UI thread, like any collection a control shows. Describing a file - reading its
/// size, probing its length - starts as it is added and finishes when it does, so a long folder
/// appears at once and fills in.
/// </remarks>
public sealed class FileList
{
    private readonly Func<string, bool> _accepts;
    private readonly Func<string, CancellationToken, Task<FileDescription?>>? _describe;

    /// <param name="accepts">Whether a file belongs in the list; any file when null. Folders are always looked into.</param>
    /// <param name="describe">
    /// Reads what the list shows under a file's name, and anything worth keeping in
    /// <see cref="FileItem.Details"/>. Runs on the caller's thread until it awaits, so do the slow
    /// part off it. Null, or an exception, leaves the file without a description: a file that cannot
    /// even be read still shows, and fails with its reason when processed.
    /// </param>
    public FileList(Func<string, bool>? accepts = null, Func<string, CancellationToken, Task<FileDescription?>>? describe = null)
    {
        _accepts = accepts ?? (_ => true);
        _describe = describe;
    }

    public ObservableCollection<FileItem> Items { get; } = [];

    /// <summary>What the list and its items say. Set it before adding files: an item keeps the texts it was made with.</summary>
    public FileListTexts Texts { get; init; } = FileListTexts.English;

    /// <summary>What the panel's picker offers, the first selected.</summary>
    public IReadOnlyList<FileType> Types { get; init; } = [FileType.All];

    /// <summary>Raised when files are added, removed or moved.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<string> Paths => Items.Select(item => item.Path).ToArray();

    /// <summary>Adds the files the list accepts, and every such file inside the folders given.</summary>
    /// <param name="paths">Files and folders, as picked or dropped.</param>
    /// <param name="recursive">Whether a folder's subfolders are looked into too.</param>
    /// <returns>How many were added: files already in the list are not added again.</returns>
    public int Add(IEnumerable<string> paths, bool recursive = true)
    {
        ArgumentNullException.ThrowIfNull(paths);

        HashSet<string> known = new(Items.Select(item => item.Path), StringComparer.OrdinalIgnoreCase);
        int added = 0;

        foreach (string path in Expand(paths, recursive))
        {
            if (!known.Add(path)) continue;

            FileItem item = new(path, Texts);
            Items.Add(item);
            _ = DescribeAsync(item);
            added++;
        }

        if (added > 0) Changed?.Invoke(this, EventArgs.Empty);
        return added;
    }

    public void Remove(FileItem item)
    {
        if (Items.Remove(item)) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (Items.Count == 0) return;

        Items.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves a file up (negative) or down (positive); nothing past either end.</summary>
    public void Move(FileItem item, int offset)
    {
        int index = Items.IndexOf(item), target = index + offset;

        if (index < 0 || target < 0 || target >= Items.Count || target == index) return;

        Items.Move(index, target);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>By name, as the person reads it.</summary>
    public void Sort()
    {
        FileItem[] sorted = Items.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();

        if (sorted.SequenceEqual(Items)) return;

        Items.Clear();
        foreach (FileItem item in sorted) Items.Add(item);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private IEnumerable<string> Expand(IEnumerable<string> paths, bool recursive)
    {
        foreach (string given in paths)
        {
            if (string.IsNullOrWhiteSpace(given)) continue;

            // One file is one entry however its path was written: «a/b.png», «a\b.png», «a\.\b.png».
            string path = Path.GetFullPath(given);

            if (Directory.Exists(path))
            {
                string[] files;

                try
                {
                    files = Directory.EnumerateFiles(path, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                        .Where(_accepts)
                        .OrderBy(file => file, StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (string file in files) yield return file;
            }
            else if (File.Exists(path) && _accepts(path))
            {
                yield return path;
            }
        }
    }

    private async Task DescribeAsync(FileItem item)
    {
        if (_describe is null) return;

        try
        {
            if (await _describe(item.Path, CancellationToken.None) is { } description)
            {
                item.Description = description.Text;
                item.Details = description.Details;
            }
        }
        catch (Exception)
        {
            // Not describable is not a reason to leave it out: processing says what is wrong with it.
        }
    }
}
