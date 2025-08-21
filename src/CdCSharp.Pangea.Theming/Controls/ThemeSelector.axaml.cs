using Avalonia;
using Avalonia.Controls;

namespace CdCSharp.Pangea.Theming.Controls;

public partial class ThemeSelector : UserControl
{
    public static readonly StyledProperty<ThemeSelectorViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<ThemeSelector, ThemeSelectorViewModel?>(nameof(ViewModel));

    public ThemeSelector() => InitializeComponent();

    public ThemeSelectorViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ViewModelProperty)
        {
            ThemeSelectorViewModel? viewModel = change.NewValue as ThemeSelectorViewModel;
            DataContext = viewModel;
        }
    }
}