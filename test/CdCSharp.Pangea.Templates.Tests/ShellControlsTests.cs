using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CdCSharp.Pangea;
using CdCSharp.Pangea.Localization.Controls;
using CdCSharp.Pangea.Theming.Abstractions;
using CdCSharp.Pangea.Theming.Controls;
using Microsoft.Extensions.DependencyInjection;
using PangeaShellApp.ViewModels;

namespace CdCSharp.Pangea.Templates.Tests;

/// <summary>
/// The shell template's screens as controls, not as view models.
/// </summary>
/// <remarks>
/// Everything else here checks what the application is made of. This checks what a user can see
/// and press, which is where a control handed no view model, or a list with nothing in it, finally
/// shows up.
/// </remarks>
public class ShellControlsTests
{
    private static IServiceProvider Services =>
        ((PangeaApplication)Application.Current!).GetServiceProvider();

    /// <summary>The shell window itself, shown. A Window cannot be hosted inside another.</summary>
    private static Window ShowShell()
    {
        PangeaShellApp.Views.MainWindow window = new()
        {
            DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            Width = 600,
            Height = 400
        };

        window.Show();
        return window;
    }

    private static Window Show(Control content)
    {
        Window window = new() { Content = content, Width = 400, Height = 400 };
        window.Show();
        return window;
    }

    [AvaloniaFact]
    public void TheThemeToggle_IsHandedItsViewModel()
    {
        Window window = ShowShell();

        ThemeSelector selector = window.GetVisualDescendants().OfType<ThemeSelector>().Single();

        Assert.NotNull(selector.ViewModel);
        Assert.NotNull(selector.DataContext);
    }

    [AvaloniaFact]
    public void TheThemeToggle_ShowsAnIconAndSwitchesTheVariant()
    {
        Window window = ShowShell();

        ToggleButton toggle = window.GetVisualDescendants()
            .OfType<ThemeSelector>().Single()
            .GetVisualDescendants().OfType<ToggleButton>().Single();

        TextBlock icon = toggle.GetVisualDescendants().OfType<TextBlock>().Single();
        Assert.False(string.IsNullOrWhiteSpace(icon.Text), "The toggle shows no icon.");

        IThemeService theming = Services.GetRequiredService<IThemeService>();
        ThemeVariant before = theming.CurrentVariant;

        toggle.IsChecked = toggle.IsChecked != true;

        Assert.NotEqual(before, theming.CurrentVariant);
    }

    [AvaloniaFact]
    public void TheLanguagePicker_OffersTheSupportedLanguages()
    {
        SettingsViewModel settings = Services.GetRequiredService<SettingsViewModel>();
        Window window = Show(new PangeaShellApp.Views.SettingsView { DataContext = settings });

        LanguageSelector selector = window.GetVisualDescendants().OfType<LanguageSelector>().Single();

        Assert.NotNull(selector.ViewModel);
        Assert.NotEmpty(selector.ViewModel!.AvailableLanguages);

        ComboBox combo = selector.GetVisualDescendants().OfType<ComboBox>().Single();

        Assert.NotNull(combo.ItemsSource);
        Assert.NotEmpty(combo.ItemsSource!.Cast<object>());
    }
}
