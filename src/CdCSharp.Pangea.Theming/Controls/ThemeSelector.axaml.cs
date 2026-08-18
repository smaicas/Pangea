using Avalonia;
using Avalonia.Controls;

namespace CdCSharp.Pangea.Theming.Controls;

/// <summary>
/// A toggle between the current theme's light and dark palettes.
/// </summary>
/// <remarks>
/// A control built by XAML gets no constructor injection, so it is handed the view model the
/// container built: <c>&lt;theming:ThemeSelector ViewModel="{Binding ThemeSelector}" /&gt;</c>
/// </remarks>
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

    /// <summary>
    /// Adopts a view model handed over as the DataContext, which is the other way this control gets
    /// used.
    /// </summary>
    /// <remarks>
    /// The reverse - assigning DataContext from ViewModel - is what this control used to do, and it
    /// could not work: <c>ViewModel</c> is bound against the DataContext inherited from whatever
    /// hosts the control, so overwriting that DataContext leaves the binding reading from an object
    /// that has no such property, and the view model it just delivered is replaced with null.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataContextProperty && change.NewValue is ThemeSelectorViewModel adopted)
        {
            ViewModel = adopted;
        }
    }
}
