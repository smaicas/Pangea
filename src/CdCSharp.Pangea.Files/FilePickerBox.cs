using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using CdCSharp.Pangea.Dialogs;

namespace CdCSharp.Pangea.Files;

/// <summary>
/// A path, typed or chosen with the system's picker: a file to open, a file to save with
/// <see cref="Save"/>, or a folder with <see cref="PickFolder"/>.
/// </summary>
/// <remarks>
/// The picker is <see cref="IFilePickerService"/>, from the application's container, and starts
/// where the path already points. Its texts are English unless set; a style sets them for a whole
/// application: <c>&lt;Style Selector="f|FilePickerBox"&gt;&lt;Setter Property="BrowseText" Value="Elegir…" /&gt;&lt;/Style&gt;</c>.
/// </remarks>
public sealed class FilePickerBox : Grid
{
    public static readonly StyledProperty<string?> PathProperty =
        AvaloniaProperty.Register<FilePickerBox, string?>(nameof(Path), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The extensions offered, besides any file; <c>png</c> or <c>.png</c>.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ExtensionsProperty =
        AvaloniaProperty.Register<FilePickerBox, IReadOnlyList<string>?>(nameof(Extensions));

    /// <summary>What the picker calls <see cref="Extensions"/>.</summary>
    public static readonly StyledProperty<string> FilterNameProperty = AvaloniaProperty.Register<FilePickerBox, string>(nameof(FilterName), "Files");

    public static readonly StyledProperty<bool> PickFolderProperty = AvaloniaProperty.Register<FilePickerBox, bool>(nameof(PickFolder));

    public static readonly StyledProperty<bool> SaveProperty = AvaloniaProperty.Register<FilePickerBox, bool>(nameof(Save));

    public static readonly StyledProperty<string?> WatermarkProperty = AvaloniaProperty.Register<FilePickerBox, string?>(nameof(Watermark));

    public static readonly StyledProperty<string> BrowseTextProperty = AvaloniaProperty.Register<FilePickerBox, string>(nameof(BrowseText), "Choose…");

    /// <summary>The picker's title; by mode when null: «Choose a folder», «Save as», «Choose a file».</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<FilePickerBox, string?>(nameof(Title));

    private readonly TextBox _text = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _browse = new() { VerticalAlignment = VerticalAlignment.Center };

    public FilePickerBox()
    {
        ColumnDefinitions = new ColumnDefinitions("*,Auto");
        ColumnSpacing = 6;

        SetColumn(_browse, 1);
        Children.Add(_text);
        Children.Add(_browse);

        _browse.Content = BrowseText;
        _text.Bind(TextBox.TextProperty, this.GetObservable(PathProperty));
        _text.LostFocus += (_, _) => Path = string.IsNullOrWhiteSpace(_text.Text) ? "" : _text.Text.Trim().Trim('"');
        _browse.Click += async (_, _) => await BrowseAsync();
    }

    public string? Path { get => GetValue(PathProperty); set => SetValue(PathProperty, value); }

    public IReadOnlyList<string>? Extensions { get => GetValue(ExtensionsProperty); set => SetValue(ExtensionsProperty, value); }

    public string FilterName { get => GetValue(FilterNameProperty); set => SetValue(FilterNameProperty, value); }

    public bool PickFolder { get => GetValue(PickFolderProperty); set => SetValue(PickFolderProperty, value); }

    public bool Save { get => GetValue(SaveProperty); set => SetValue(SaveProperty, value); }

    public string? Watermark { get => GetValue(WatermarkProperty); set => SetValue(WatermarkProperty, value); }

    public string BrowseText { get => GetValue(BrowseTextProperty); set => SetValue(BrowseTextProperty, value); }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WatermarkProperty) _text.PlaceholderText = Watermark;
        else if (change.Property == BrowseTextProperty) _browse.Content = BrowseText;
    }

    /// <summary>What the picker offers: the extensions named, then any file.</summary>
    internal IReadOnlyList<FileType> Types() =>
        Extensions is { Count: > 0 } extensions ? [new FileType(FilterName, extensions), FileType.All] : [FileType.All];

    private async Task BrowseAsync()
    {
        if ((Application.Current as PangeaApplication)?.GetServiceProvider().GetService(typeof(IFilePickerService)) is not IFilePickerService pickers) return;

        string? current = string.IsNullOrWhiteSpace(Path) ? null : Path;
        string? chosen;

        if (PickFolder)
        {
            chosen = await pickers.OpenFolderAsync(Title ?? "Choose a folder", current);
        }
        else if (Save)
        {
            chosen = await pickers.SaveFileAsync(Title ?? "Save as", current is null ? "" : System.IO.Path.GetFileName(current), Types(), current);
        }
        else
        {
            chosen = await pickers.OpenFileAsync(Title ?? "Choose a file", Types(), current);
        }

        if (chosen is not null) Path = chosen;
    }
}
