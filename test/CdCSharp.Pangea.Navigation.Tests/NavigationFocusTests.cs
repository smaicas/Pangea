using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Navigation.Tests.Infrastructure;
using System.Reflection;

namespace CdCSharp.Pangea.Navigation.Tests;

/// <summary>
/// Where keyboard focus lands when a screen is replaced.
/// </summary>
/// <remarks>
/// The host used to leave it nowhere. The element the user was on is removed from the tree with the
/// old screen, and if nothing takes over, the next Tab starts from the top of the window - so
/// anyone not using a mouse is put back to the beginning on every navigation.
/// </remarks>
public class NavigationFocusTests
{
    public sealed class TwoButtonsView : UserControl
    {
        public TwoButtonsView()
        {
            First = new Button { Content = "first" };
            Second = new Button { Content = "second" };

            StackPanel panel = new();
            panel.Children.Add(First);
            panel.Children.Add(Second);
            Content = panel;
        }

        public Button First { get; }

        public Button Second { get; }
    }

    public sealed class TwoButtonsViewModel;

    public sealed class NothingToFocusView : UserControl
    {
        public NothingToFocusView() => Content = new TextBlock { Text = "read only" };
    }

    public sealed class NothingToFocusViewModel;

    private sealed class Inline : IUIDispatcher
    {
        public bool CheckAccess() => true;
        public void Post(Action action) => action();
        public void Invoke(Action action) => action();
        public T Invoke<T>(Func<T> callback) => callback();
        public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
        public Task<T> InvokeAsync<T>(Func<Task<T>> callback) => callback();
    }

    private static (NavigationHost Host, NavigationService Navigation, Window Window) Attach(
        bool movesFocus = true)
    {
        StubServices services = new();
        NavigationService navigation = new(services, new Inline());

        TypeRegistry registry = new([Assembly.GetExecutingAssembly()]);
        registry.Initialize();

        NavigationHost host = new()
        {
            Service = navigation,
            Locator = new ViewLocator(services, registry),
            MovesFocusOnNavigation = movesFocus
        };

        Window window = new() { Content = host, Width = 400, Height = 300 };
        window.Show();
        window.Activate();
        Pump();

        return (host, navigation, window);
    }

    /// <summary>Focus is placed once layout has run, so the test has to let that happen.</summary>
    private static void Pump()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }
    }

    private static object? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    [AvaloniaFact]
    public async Task ArrivingAtAScreen_PutsFocusOnItsFirstControl()
    {
        (_, NavigationService navigation, Window window) = Attach();

        await navigation.NavigateToAsync<TwoButtonsViewModel>();
        Pump();

        Button focused = Assert.IsType<Button>(Focused(window));
        Assert.Equal("first", focused.Content);
        window.Close();
    }

    [AvaloniaFact]
    public async Task NavigatingOn_MovesFocusToTheNewScreenRatherThanLosingIt()
    {
        (NavigationHost host, NavigationService navigation, Window window) = Attach();

        await navigation.NavigateToAsync<TwoButtonsViewModel>();
        Pump();

        // The user tabs along before leaving.
        ((TwoButtonsView)host.Content!).Second.Focus();
        Pump();

        await navigation.NavigateToAsync<NothingToFocusViewModel>();
        Pump();

        Assert.NotNull(Focused(window));
        window.Close();
    }

    [AvaloniaFact]
    public async Task GoingBack_PutsFocusInTheScreenReturnedTo()
    {
        (_, NavigationService navigation, Window window) = Attach();

        await navigation.NavigateToAsync<TwoButtonsViewModel>();
        await navigation.NavigateToAsync<NothingToFocusViewModel>();
        Pump();

        await navigation.GoBackAsync();
        Pump();

        Button focused = Assert.IsType<Button>(Focused(window));
        Assert.Equal("first", focused.Content);
        window.Close();
    }

    /// <summary>
    /// A screen with nothing to act on still needs somewhere for focus to sit, or Tab has no
    /// starting point and a screen reader has nothing to announce.
    /// </summary>
    [AvaloniaFact]
    public async Task AScreenWithNothingFocusable_FallsBackToTheHost()
    {
        (NavigationHost host, NavigationService navigation, Window window) = Attach();

        await navigation.NavigateToAsync<NothingToFocusViewModel>();
        Pump();

        Assert.Same(host, Focused(window));
        window.Close();
    }

    /// <summary>
    /// The way out for a host that is not the main subject of the screen - a detail pane beside a
    /// list, where taking focus off the list on every selection would be maddening.
    /// </summary>
    [AvaloniaFact]
    public async Task AHostThatIsAskedNotTo_LeavesFocusAlone()
    {
        (_, NavigationService navigation, Window window) = Attach(movesFocus: false);

        await navigation.NavigateToAsync<TwoButtonsViewModel>();
        Pump();

        Assert.Null(Focused(window));
        window.Close();
    }
}
