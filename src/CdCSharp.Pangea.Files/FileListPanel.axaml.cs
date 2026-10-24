using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Platform;
using System.Collections.Specialized;

namespace CdCSharp.Pangea.Files;

/// <summary>
/// A <see cref="FileList"/> on screen: add with the pickers or by dropping files and folders, sort,
/// reorder, remove, and see how each file went.
/// </summary>
/// <remarks>
/// The pickers are the list's <see cref="FileList.Types"/> through <see cref="IFilePickerService"/>,
/// and "show the result" is <see cref="ISystemLauncher"/>, both from the application's container.
/// It wears the classes and icons of <c>PangeaStyles</c>.
/// </remarks>
public partial class FileListPanel : UserControl
{
    public static readonly StyledProperty<FileList?> FilesProperty = AvaloniaProperty.Register<FileListPanel, FileList?>(nameof(Files));

    /// <summary>Shows the up and down buttons, for lists whose order is the result's: frames, pages, a playlist.</summary>
    public static readonly StyledProperty<bool> ReorderableProperty = AvaloniaProperty.Register<FileListPanel, bool>(nameof(Reorderable));

    /// <summary>What the empty list says here; the list's texts when null.</summary>
    public static readonly StyledProperty<string?> EmptyTextProperty = AvaloniaProperty.Register<FileListPanel, string?>(nameof(EmptyText));

    /// <summary>The file selected, when one is: for a screen that shows the details of one.</summary>
    public static readonly StyledProperty<FileItem?> SelectedItemProperty =
        AvaloniaProperty.Register<FileListPanel, FileItem?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    public FileListPanel()
    {
        InitializeComponent();

        AddFiles.Click += async (_, _) => await AddFilesAsync();
        AddFolder.Click += async (_, _) => await AddFolderAsync();
        SortButton.Click += (_, _) => Files?.Sort();
        ClearButton.Click += (_, _) => Files?.Clear();

        DropArea.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DropArea.AddHandler(DragDrop.DropEvent, OnDrop);
        List.KeyDown += OnListKeyDown;
        List.SelectionChanged += (_, _) => SelectedItem = List.SelectedItems?.Count == 1 ? List.SelectedItem as FileItem : null;

        UpdateCount();
    }

    public FileList? Files { get => GetValue(FilesProperty); set => SetValue(FilesProperty, value); }

    public bool Reorderable { get => GetValue(ReorderableProperty); set => SetValue(ReorderableProperty, value); }

    public string? EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    public FileItem? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FilesProperty)
        {
            if (change.OldValue is FileList old) old.Items.CollectionChanged -= OnItemsChanged;

            if (Files is { } files) files.Items.CollectionChanged += OnItemsChanged;

            List.ItemsSource = Files?.Items;
            UpdateCount();
        }
        else if (change.Property == EmptyTextProperty)
        {
            UpdateCount();
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateCount();

    private void UpdateCount()
    {
        FileListTexts texts = Files?.Texts ?? FileListTexts.English;
        int count = Files?.Items.Count ?? 0;

        Count.Text = texts.Count(count);
        Empty.IsVisible = count == 0;
        EmptyLabel.Text = EmptyText ?? texts.Empty;
    }

    private async Task AddFilesAsync()
    {
        if (Files is not { } files || Service<IFilePickerService>() is not { } pickers) return;

        files.Add(await pickers.OpenFilesAsync(files.Texts.AddFilesTitle, files.Types));
    }

    private async Task AddFolderAsync()
    {
        if (Files is not { } files || Service<IFilePickerService>() is not { } pickers) return;

        if (await pickers.OpenFolderAsync(files.Texts.AddFolderTitle) is { } folder) files.Add([folder]);
    }

    private void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = Files is not null && e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (Files is null || e.DataTransfer.TryGetFiles() is not { } dropped) return;

        Files.Add(dropped.Select(item => item.TryGetLocalPath()).OfType<string>());
        e.Handled = true;
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || Files is null) return;

        foreach (FileItem item in List.SelectedItems?.OfType<FileItem>().ToArray() ?? []) Files.Remove(item);
    }

    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        if (Item(sender) is { } item) Files?.Remove(item);
    }

    private void OnUp(object? sender, RoutedEventArgs e)
    {
        if (Item(sender) is { } item) Files?.Move(item, -1);
    }

    private void OnDown(object? sender, RoutedEventArgs e)
    {
        if (Item(sender) is { } item) Files?.Move(item, 1);
    }

    private void OnShowResult(object? sender, RoutedEventArgs e)
    {
        if (Item(sender)?.OutputPath is { } path && Service<ISystemLauncher>() is { } launcher) _ = launcher.ShowInFolderAsync(path);
    }

    private static FileItem? Item(object? sender) => (sender as Control)?.DataContext as FileItem;

    /// <summary>From the application's container: a control is not built by it, and has no constructor to ask in.</summary>
    private static T? Service<T>() where T : class =>
        (Application.Current as PangeaApplication)?.GetServiceProvider().GetService(typeof(T)) as T;
}
