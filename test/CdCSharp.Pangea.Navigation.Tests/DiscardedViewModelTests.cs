using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Navigation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Navigation.Tests;

/// <summary>
/// What happens to a screen the stack lets go of.
/// </summary>
/// <remarks>
/// A screen that subscribed to a service on the way in is kept alive by that service's event list
/// on the way out, and nothing about it is visible until an application has been used for a while.
/// Navigation is the only thing that knows a screen is finished, so it is what says so.
/// </remarks>
public class DiscardedViewModelTests
{
    private abstract class Screen : IDiscardable
    {
        public int Discards { get; private set; }

        public void Discard() => Discards++;
    }

    private sealed class First : Screen;

    private sealed class Second : Screen;

    private sealed class Services : IServiceProvider
    {
        private readonly Dictionary<Type, object> _resolved = [];

        public object? GetService(Type serviceType)
        {
            // One instance per type, so a test can look at what navigation dropped.
            if (_resolved.TryGetValue(serviceType, out object? existing)) return existing;

            object created = Activator.CreateInstance(serviceType)!;
            _resolved[serviceType] = created;
            return created;
        }

        public T Resolve<T>() => (T)GetService(typeof(T))!;
    }

    /// <summary>Runs everything where it was called: there is no UI thread in these tests.</summary>
    private sealed class ImmediateDispatcher : IUIDispatcher
    {
        public bool CheckAccess() => true;

        public void Post(Action action) => action();

        public void Invoke(Action action) => action();

        public T Invoke<T>(Func<T> callback) => callback();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<Task<T>> callback) => callback();
    }

    private static NavigationService Create(Services services, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        PangeaOptions options = PangeaOptions.Default;
        options.DI.ViewModelLifetime = lifetime;

        return new NavigationService(services, new ImmediateDispatcher(), Options.Create(options));
    }

    /// <summary>
    /// Forward is not a discard: the screen goes on the history stack, and going back is expected
    /// to find it as it was left.
    /// </summary>
    [Fact]
    public async Task NavigatingForward_LeavesTheScreenAlone()
    {
        Services services = new();
        NavigationService navigation = Create(services);

        await navigation.NavigateToAsync<First>();
        await navigation.NavigateToAsync<Second>();

        Assert.Equal(0, services.Resolve<First>().Discards);
    }

    [Fact]
    public async Task GoingBack_DiscardsTheScreenBeingLeft()
    {
        Services services = new();
        NavigationService navigation = Create(services);

        await navigation.NavigateToAsync<First>();
        await navigation.NavigateToAsync<Second>();
        await navigation.GoBackAsync();

        Assert.Equal(1, services.Resolve<Second>().Discards);
        Assert.Equal(0, services.Resolve<First>().Discards);
        Assert.IsType<First>(navigation.CurrentViewModel);
    }

    [Fact]
    public async Task ClearingTheHistory_DiscardsWhatWasOnIt()
    {
        Services services = new();
        NavigationService navigation = Create(services);

        await navigation.NavigateToAsync<First>();
        await navigation.NavigateToAsync<Second>();

        navigation.ClearHistory();

        Assert.Equal(1, services.Resolve<First>().Discards);

        // The current screen is not on the stack and is still on screen.
        Assert.Equal(0, services.Resolve<Second>().Discards);
    }

    /// <summary>
    /// A view model the container hands out again is not one navigation may take apart: the next
    /// navigation to it would get the same instance, with its subscriptions released.
    /// </summary>
    [Fact]
    public async Task WithSingletonViewModels_NothingIsDiscarded()
    {
        Services services = new();
        NavigationService navigation = Create(services, ServiceLifetime.Singleton);

        await navigation.NavigateToAsync<First>();
        await navigation.NavigateToAsync<Second>();
        await navigation.GoBackAsync();
        navigation.ClearHistory();

        Assert.Equal(0, services.Resolve<First>().Discards);
        Assert.Equal(0, services.Resolve<Second>().Discards);
    }
}
