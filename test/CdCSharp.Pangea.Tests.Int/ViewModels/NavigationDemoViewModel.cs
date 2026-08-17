using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Navigation.Abstractions;
using CdCSharp.Pangea.Tests.Int.ViewModels.Navigation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace CdCSharp.Pangea.Tests.Int.ViewModels;

/// <summary>
/// Drives the navigation demo window. Deliberately does nothing clever: the buttons call the
/// service, and the host in the window follows on its own.
/// </summary>
public partial class NavigationDemoViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    [Binding(ReadOnly = true)] private string _lastResult = "Nothing yet.";

    public NavigationDemoViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _navigation = serviceProvider.GetRequiredService<INavigationService>();
        _navigation.PropertyChanged += OnNavigationChanged;
    }

    public RelayCommand GoHomeCommand => CreateCommand(() => Navigate(_navigation.NavigateToAsync<HomeViewModel>()));

    public RelayCommand GoToOrderCommand => CreateCommand(() =>
        Navigate(_navigation.NavigateToAsync(new ShowOrderDetail(Guid.NewGuid(), "Grace Hopper"))));

    public RelayCommand GoToSettingsCommand =>
        CreateCommand(() => Navigate(_navigation.NavigateToAsync<SettingsViewModel>()));

    public RelayCommand GoBackCommand => CreateCommand(() => Navigate(_navigation.GoBackAsync()), () => CanGoBack);

    public bool CanGoBack => _navigation.CanGoBack;

    public string CurrentScreen => _navigation.CurrentViewModel?.GetType().Name ?? "(none)";

    private async Task Navigate(Task<bool> navigation)
    {
        bool moved = await navigation;

        _lastResult = moved
            ? $"Navigated. Now showing {CurrentScreen}."
            : "Cancelled: the current screen refused to be navigated away from.";

        OnPropertyChanged(nameof(LastResult));
    }

    private void OnNavigationChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CurrentScreen));
        OnPropertyChanged(nameof(CanGoBack));
        GoBackCommand.RaiseCanExecuteChanged();
    }
}
