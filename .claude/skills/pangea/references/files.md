# Working through files: CdCSharp.Pangea.Files

A screen that takes files - picked or dropped, folders included - and does something to each:
converts, shrinks, renames. Installed on its own: `dotnet add package CdCSharp.Pangea.Files`.

| Piece | What it is |
|---|---|
| `FileList` | The files: added without repeats, every accepted file inside a folder, described as they come |
| `FileItem` | One file and how it went: status, progress, result or error, ready to bind |
| `FileListPanel` | The list on screen: add, drop, sort, reorder, remove, show the result |
| `FilePickerBox` | A path typed or chosen: a file to open, one to save, or a folder |
| `OutputNames` | Names from a template, claimed so two parallel workers never share one |
| `FileBatch` | Runs the list through one piece of work, a few at a time, with counts, progress and cancel |

## A screen

```csharp
using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Files;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace FilesSamples;

public partial class ShrinkViewModel : ViewModelBase
{
    private readonly FileBatch _batch;

    public ShrinkViewModel(IServiceProvider services) : base(services)
    {
        _batch = new FileBatch(services.GetRequiredService<IUIDispatcher>());
        Subscribe(handler => _batch.Changed += handler, handler => _batch.Changed -= handler,
            (_, _) => Summary = $"{_batch.Done} done, {_batch.Failed} failed");
    }

    // Accepted files only, and folders looked into; what describe returns shows under each name.
    public FileList Files { get; } = new(
        accepts: path => Path.GetExtension(path) is ".png" or ".jpg",
        describe: async (path, cancellationToken) =>
        {
            long length = await Task.Run(() => new FileInfo(path).Length, cancellationToken);
            return new FileDescription(length > 1_000_000 ? "large" : "small");
        })
    {
        Types = [new FileType("Images", ["png", "jpg"]), FileType.All]
    };

    [Binding] private string _outputFolder = "";
    [Binding] private string _summary = "";

    public RelayCommand ShrinkCommand => CreateCommand(() => _batch.RunAsync(Files.Items, async work =>
    {
        // Claimed: created empty now, deleted again if this file fails or the batch is cancelled.
        string? output = work.Resolve("{name}-small", OutputFolder, ".png", ConflictPolicy.Rename);
        if (output is null) return FileOutcome.Skipped();

        await using FileStream source = File.OpenRead(work.Item.Path);
        await using FileStream target = File.Create(output);
        await source.CopyToAsync(target, work.CancellationToken);

        work.Progress.Report(1);
        return FileOutcome.Written(output, FileSize.Text(target.Length));
    }, parallelism: 2));

    public RelayCommand CancelCommand => CreateCommand(_batch.Cancel);
}
```

```xml
<UserControl xmlns:files="using:CdCSharp.Pangea.Files">
  <DockPanel>
    <files:FilePickerBox DockPanel.Dock="Top" Path="{Binding OutputFolder}" PickFolder="True" />
    <files:FileListPanel Files="{Binding Files}" EmptyText="Drop images here" />
  </DockPanel>
</UserControl>
```

## Rules that matter

- **The panel wears `PangeaStyles`** - its classes and `Icon.*` - so put `<theming:PangeaStyles />` in
  `Application.Styles` (see `references/theming.md`). Without it the panel works, unstyled.
- **Texts are English** until the list is given its own: `Texts = FileListTexts.English with { AddFiles = "Añadir archivos", Done = "Hecho" }`.
  Set them when the list is made: an item keeps the texts it was made with. `FilePickerBox` has
  `BrowseText` and `Title` properties, which a style sets for the whole application.
- **Describe does the slow part off the UI thread** - `Task.Run`, an async probe - and returns what
  to show; a failure leaves the file without a description, and processing says what is wrong.
- **One file, one entry**: paths are made absolute before comparing, so a file dropped twice, or
  reached through two folders, is listed once.
- **Process throws to fail its file**; the rest carry on. The message is the exception's unless
  `DescribeError` says otherwise. Cancelling stops the files running and marks the rest Canceled.
- **Claim names through `work.Claim` or `work.Resolve`**, not `OutputNames` directly: the batch then
  deletes what a file claimed and did not come to - and any claim its outcome does not name - so a
  failed run leaves no empty files. `ConflictPolicy.Overwrite` claims the existing file, which is
  never deleted.
- **The items change on the UI thread**, through the dispatcher the batch was given;
  `Done`, `Failed`, `Skipped`, `BytesBefore`/`BytesAfter` and `LastFolder` are safe to read from any.
- **In a host**, wrap the run in `AppActivity.Begin(...)` (see `references/apps.md`) so the host's
  request to close waits for it.
