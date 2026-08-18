using Avalonia;
using Avalonia.Controls;

namespace CdCSharp.Pangea.Localization.Controls;

/// <summary>
/// A picker for the application's language.
/// </summary>
/// <remarks>
/// A control built by XAML gets no constructor injection, so it is handed the view model the
/// container built - the same arrangement as the theme selector next door:
/// <code>&lt;loc:LanguageSelector ViewModel="{Binding LanguageSelector}" /&gt;</code>
/// </remarks>
public partial class LanguageSelector : UserControl
{
    public static readonly StyledProperty<LanguageSelectorViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<LanguageSelector, LanguageSelectorViewModel?>(nameof(ViewModel));

    public LanguageSelector() => InitializeComponent();

    public LanguageSelectorViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ViewModelProperty)
        {
            DataContext = change.NewValue as LanguageSelectorViewModel;
        }
    }
}
