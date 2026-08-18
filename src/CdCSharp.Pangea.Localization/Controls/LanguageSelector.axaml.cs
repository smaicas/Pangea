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

    /// <summary>
    /// Adopts a view model handed over as the DataContext, which is the other way this control gets
    /// used.
    /// </summary>
    /// <remarks>
    /// The reverse - assigning DataContext from ViewModel - cannot work: <c>ViewModel</c> is bound
    /// against the DataContext inherited from whatever hosts the control, so overwriting that
    /// DataContext leaves the binding reading from an object that has no such property, and the
    /// view model it just delivered is replaced with null.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataContextProperty && change.NewValue is LanguageSelectorViewModel adopted)
        {
            ViewModel = adopted;
        }
    }
}
