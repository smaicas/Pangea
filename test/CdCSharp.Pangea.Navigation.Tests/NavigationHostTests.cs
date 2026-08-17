using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Navigation.Tests.Infrastructure;
using System.Reflection;

namespace CdCSharp.Pangea.Navigation.Tests;

/// <summary>
/// The host is the whole user-facing surface: a control in a window that follows the service.
/// </summary>
public class NavigationHostTests
{
    private static (NavigationHost Host, NavigationService Navigation, Window Window) Attach()
    {
        StubServices services = new();
        NavigationService navigation = new(services, new InlineDispatcher());

        TypeRegistry registry = new([Assembly.GetExecutingAssembly()]);
        registry.Initialize();

        NavigationHost host = new()
        {
            Service = navigation,
            Locator = new ViewLocator(services, registry)
        };

        Window window = new() { Content = host };
        window.Show();

        return (host, navigation, window);
    }

    [AvaloniaFact]
    public async Task TheHostShowsTheViewForTheCurrentViewModel()
    {
        (NavigationHost host, NavigationService navigation, Window window) = Attach();

        await navigation.NavigateToAsync(new ShowOrder(Guid.NewGuid()));

        Assert.IsType<OrderView>(host.Content);
        window.Close();
    }

    [AvaloniaFact]
    public async Task TheHostFollowsEveryNavigation()
    {
        (NavigationHost host, NavigationService navigation, Window window) = Attach();

        await navigation.NavigateToAsync(new ShowOrder(Guid.NewGuid()));
        await navigation.NavigateToAsync(new ShowReport("q1"));

        Assert.IsType<ReportView>(host.Content);

        await navigation.GoBackAsync();

        Assert.IsType<OrderView>(host.Content);
        window.Close();
    }

    [AvaloniaFact]
    public async Task TheViewIsBoundToTheViewModelThatWasNavigatedTo()
    {
        (NavigationHost host, NavigationService navigation, Window window) = Attach();

        await navigation.NavigateToAsync(new ShowOrder(Guid.NewGuid()));

        Control view = Assert.IsType<OrderView>(host.Content);
        Assert.Same(navigation.CurrentViewModel, view.DataContext);
        window.Close();
    }

    [AvaloniaFact]
    public async Task AHostAttachedAfterTheFactCatchesUp()
    {
        StubServices services = new();
        NavigationService navigation = new(services, new InlineDispatcher());

        // Navigation happens before the host exists, which is what a window opened later does.
        await navigation.NavigateToAsync(new ShowOrder(Guid.NewGuid()));

        TypeRegistry registry = new([Assembly.GetExecutingAssembly()]);
        registry.Initialize();

        NavigationHost host = new()
        {
            Service = navigation,
            Locator = new ViewLocator(services, registry)
        };

        Window window = new() { Content = host };
        window.Show();

        Assert.IsType<OrderView>(host.Content);
        window.Close();
    }

    /// <summary>
    /// A host left subscribed to a long-lived service keeps every closed window's content alive.
    /// </summary>
    [AvaloniaFact]
    public async Task AClosedWindowsHostStopsFollowingTheService()
    {
        (NavigationHost host, NavigationService navigation, Window window) = Attach();

        await navigation.NavigateToAsync(new ShowOrder(Guid.NewGuid()));
        window.Close();

        await navigation.NavigateToAsync(new ShowReport("q1"));

        Assert.IsType<OrderView>(host.Content);
    }

    private sealed class InlineDispatcher : IUIDispatcher
    {
        public bool CheckAccess() => true;

        public void Post(Action action) => action();

        public void Invoke(Action action) => action();

        public Task<T> InvokeAsync<T>(Func<Task<T>> callback) => callback();
    }
}
