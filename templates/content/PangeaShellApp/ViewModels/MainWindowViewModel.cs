using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Localization;
using CdCSharp.Pangea.Navigation.Abstractions;
using CdCSharp.Pangea.Theming.Controls;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace PangeaShellApp.ViewModels;

/// <summary>
/// The shell: a menu, a back button, and a <c>NavigationHost</c> that shows whatever the
/// navigation service currently points at.
/// </summary>
/// <remarks>
/// It holds no screen of its own. Everything the user sees on the right belongs to the view model
/// the service is on, which is why this class never mentions one except to navigate to it.
/// </remarks>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    public MainWindowViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _navigation = serviceProvider.GetRequiredService<INavigationService>();
        Strings = serviceProvider.GetRequiredService<LocalizedStrings>();
        ThemeSelector = serviceProvider.GetRequiredService<ThemeSelectorViewModel>();

        _navigation.PropertyChanged += OnNavigationChanged;

        // The first screen. Runs inline: the shell is built on the UI thread.
        _ = _navigation.NavigateToAsync<HomeViewModel>();
    }

    /// <summary>The application's strings, for the menu to bind to.</summary>
    public LocalizedStrings Strings { get; }

    /// <summary>Drives the light/dark toggle in the title bar.</summary>
    public ThemeSelectorViewModel ThemeSelector { get; }

    public bool CanGoBack => _navigation.CanGoBack;

    public RelayCommand GoHomeCommand => CreateCommand(() => _navigation.NavigateToAsync<HomeViewModel>());

    public RelayCommand GoToSettingsCommand => CreateCommand(() => _navigation.NavigateToAsync<SettingsViewModel>());

    public RelayCommand GoBackCommand => CreateCommand(() => _navigation.GoBackAsync(), () => CanGoBack);

    /// <summary>
    /// The service owns the history, so the button follows it rather than the other way round:
    /// a screen that navigates on its own still updates the shell.
    /// </summary>
    private void OnNavigationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(INavigationService.CanGoBack)) return;

        OnPropertyChanged(nameof(CanGoBack));
        GoBackCommand.RaiseCanExecuteChanged();
    }
}
