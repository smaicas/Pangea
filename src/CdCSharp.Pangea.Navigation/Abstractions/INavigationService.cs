using CdCSharp.Pangea.Core.Abstractions;
using System.ComponentModel;

namespace CdCSharp.Pangea.Navigation.Abstractions;

/// <summary>
/// Moves the current view model, and with it whatever a <c>NavigationHost</c> is showing.
/// </summary>
/// <remarks>
/// One stack for the application. <see cref="INotifyPropertyChanged"/> is raised for
/// <see cref="CurrentViewModel"/> and <see cref="CanGoBack"/> so XAML can bind to both.
/// </remarks>
public interface INavigationService : INotifyPropertyChanged
{
    /// <summary>The view model being displayed, or <see langword="null"/> before the first navigation.</summary>
    object? CurrentViewModel { get; }

    /// <summary>Whether there is anywhere to go back to.</summary>
    bool CanGoBack { get; }

    /// <summary>Navigates to <typeparamref name="TViewModel"/> with no request.</summary>
    /// <returns><see langword="false"/> if the current view model refused to be navigated away from.</returns>
    Task<bool> NavigateToAsync<TViewModel>() where TViewModel : class;

    /// <summary>
    /// Navigates to the view model named by <paramref name="request"/>, which is handed to it typed.
    /// </summary>
    /// <returns><see langword="false"/> if the current view model refused to be navigated away from.</returns>
    Task<bool> NavigateToAsync<TViewModel>(INavigationRequest<TViewModel> request) where TViewModel : class;

    /// <summary>Returns to the previous entry.</summary>
    /// <returns><see langword="false"/> if there was nothing to go back to, or the current view model refused.</returns>
    Task<bool> GoBackAsync();

    /// <summary>Forgets the history without touching what is displayed.</summary>
    void ClearHistory();
}
