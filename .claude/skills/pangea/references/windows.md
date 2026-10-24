# Windows and dialogs

What a window has to get right beyond its contents. Read this before writing one.

## Windows have to fit on smaller screens

A window is opened at a size that suits the developer's monitor and then used on someone else's.
Make the part that holds content scroll, and **choose where the scroll goes** rather than wrapping
the whole window: inside a `ScrollViewer` the available height becomes infinite, so `*` rows
collapse and anything that stretches stops stretching.

```xml
<Grid RowDefinitions="Auto,*">
  <Border Grid.Row="0"> <!-- header, stays put --> </Border>

  <ScrollViewer Grid.Row="1">   <!-- only the content region scrolls -->
    <StackPanel> <!-- ... --> </StackPanel>
  </ScrollViewer>
</Grid>
```

For a side panel next to a filling region, scroll the panel alone and leave the region to fill.
The toolkit does not do this for you, deliberately: only the author knows which part should scroll
and which should stretch.

## Keyboard, for windows and dialogs alike

- **Both get focus placed for them** when they open, on the first control that can take it - unless
  the window focused something itself, which is always respected.
- **Escape closes a dialog. It does not close a window**, and that is the platform convention, not
  an oversight: Alt+F4 closes windows, and Escape destroying one holding unsaved work would be a
  keystroke away from losing it. For a secondary window where Alt+F4 feels absurd, ask for it:

```xml
<Window xmlns:win="using:CdCSharp.Pangea.Windows"
        win:WindowBehavior.CloseOnEscape="True">
```

## What a dialog is for

`IDialogService` asks two questions - a confirmation and a statement - and nothing else. For a
dialog with its own fields, or a result that is not a bool, write a view model and a window and use
`IWindowManager.ShowDialogAsync<TWindow, TViewModel, TResult>`.

Dismissing a dialog by its window chrome is read as a cancel, so `ConfirmAsync` returns `false`. It
never returns `true` without the user saying so. A dialog needs a main window to own it, and says so
if there is none. It takes the application's theme; there is nothing to style.

## Files, folders, and handing things to the system

A view model never looks for a window to get at its `StorageProvider`, and never starts a process.
It asks the container for `IFilePickerService` and `ISystemLauncher`, both registered for you:

```csharp
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Platform;
using System.IO;

namespace MyApp.ViewModels.Files;

public partial class ExportViewModel : ViewModelBase
{
    private static readonly FileType Png = new("PNG", ["png"]);

    private readonly IFilePickerService _pickers;
    private readonly ISystemLauncher _launcher;

    public ExportViewModel(IServiceProvider services, IFilePickerService pickers, ISystemLauncher launcher)
        : base(services)
    {
        _pickers = pickers;
        _launcher = launcher;
    }

    public string? LastFolder { get; private set; }

    public RelayCommand AddCommand => CreateCommand(async () =>
    {
        // Several files; the first type is the one selected. An empty list means the person cancelled.
        IReadOnlyList<string> files = await _pickers.OpenFilesAsync("Add images", [Png, FileType.All], LastFolder);

        if (files.Count > 0) LastFolder = Path.GetDirectoryName(files[0]);
    });

    public RelayCommand ExportCommand => CreateCommand(async () =>
    {
        // Offers to replace a file that exists; adds .png when the person types no extension.
        if (await _pickers.SaveFileAsync("Export", "picture.png", [Png], LastFolder) is not { } path) return;

        await File.WriteAllBytesAsync(path, []);
        await _launcher.ShowInFolderAsync(path);
    });
}
```

- Paths are local paths. A picker answers "nothing chosen" - `null`, or an empty list - when the
  person cancels, when there is no window or view to own it yet, and for an item the platform hands
  over as content rather than as a file, as Android can.
- `startIn` takes a folder or a file whose folder to start in; one that does not exist is ignored.
- Extensions are written however is at hand - `png`, `.png`, `*.png`; `FileType.All` is any file.
- `ISystemLauncher` answers whether the system took it and never throws: `OpenAsync` opens a file
  with its application or a folder in the file manager, `ShowInFolderAsync` selects a file in the
  file manager where the platform can (Windows, macOS, freedesktop file managers on Linux) and
  opens its folder elsewhere, `OpenUriAsync` follows a link.
- In tests, `PangeaTestServices.FilePickers` answers from a script - `Choosing(paths)`,
  `Cancelling()`, and a cancel once the script runs out - and `PangeaTestServices.Launcher` records
  what would have been opened.
