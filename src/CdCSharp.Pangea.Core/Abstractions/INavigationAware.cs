namespace CdCSharp.Pangea.Core.Abstractions;

/// <summary>
/// Lifecycle callbacks a navigation target receives. Implemented by <c>ViewModelBase</c>, so every
/// view model gets them; override the ones you need.
/// </summary>
public interface INavigationAware
{
    /// <summary>Called after this view model becomes the current one.</summary>
    /// <remarks>
    /// A view model navigated to with a request receives <see cref="INavigationAware{TRequest}"/>
    /// instead. Implement both if the screen is reachable with and without data.
    /// </remarks>
    Task OnNavigatedToAsync();

    /// <summary>Called after this view model stops being the current one.</summary>
    Task OnNavigatedFromAsync();

    /// <summary>
    /// Called before navigating away. Returning <see langword="false"/> cancels the navigation,
    /// which is how a screen holds on to unsaved work.
    /// </summary>
    Task<bool> CanNavigateAwayAsync();
}

/// <summary>
/// Receives the request that navigated here, typed. Implement it for the request types this view
/// model can be reached with.
/// </summary>
/// <typeparam name="TRequest">The request this view model accepts.</typeparam>
public interface INavigationAware<in TRequest> where TRequest : class
{
    /// <summary>Called after this view model becomes the current one.</summary>
    Task OnNavigatedToAsync(TRequest request);
}

/// <summary>
/// A navigation request, naming the view model it leads to. Declaring the destination on the
/// request is what lets <c>NavigateToAsync(new ShowOrder(id))</c> infer where it goes.
/// </summary>
/// <example>
/// <code>
/// public sealed record ShowOrder(Guid Id) : INavigationRequest&lt;OrderViewModel&gt;;
/// </code>
/// </example>
/// <typeparam name="TViewModel">The view model this request navigates to.</typeparam>
#pragma warning disable CA1040 // A marker is the point: it carries the destination in its type argument
public interface INavigationRequest<TViewModel> where TViewModel : class;
#pragma warning restore CA1040
