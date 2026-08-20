using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Navigation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace PangeaMobileApp.ViewModels;

/// <summary>
/// The shell: a back arrow, a title, and whatever the navigation service is pointing at.
/// </summary>
/// <remarks>
/// Named <c>MainViewModel</c> rather than <c>MainWindowViewModel</c> because the shell on a phone
/// is a view, not a window. The desktop head shows the same view inside a <c>MainWindow</c>, so one
/// shell serves both.
/// </remarks>
public partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    public MainViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _navigation = serviceProvider.GetRequiredService<INavigationService>();

        // Through Subscribe, so the service lets go of this when it is discarded. A view
        // model is transient: subscribing directly leaves every screen ever opened alive in
        // the singleton's event list.
        Subscribe(_navigation, OnNavigationChanged);

        // The first screen. Runs inline: the shell is built on the UI thread.
        _ = _navigation.NavigateToAsync<HomeViewModel>();
    }

    public bool CanGoBack => _navigation.CanGoBack;

    public RelayCommand GoBackCommand => CreateCommand(() => _navigation.GoBackAsync(), () => CanGoBack);

    /// <summary>
    /// The service owns the history, so the button follows it rather than the other way round: a
    /// screen that navigates on its own still updates the shell.
    /// </summary>
    private void OnNavigationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(INavigationService.CanGoBack)) return;

        OnPropertyChanged(nameof(CanGoBack));
        GoBackCommand.RaiseCanExecuteChanged();
    }
}
