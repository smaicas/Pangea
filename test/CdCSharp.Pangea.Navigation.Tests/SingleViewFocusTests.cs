using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Navigation.Tests.Infrastructure;
using System.Reflection;

namespace CdCSharp.Pangea.Navigation.Tests;

/// <summary>
/// Where focus goes on a shell that has no keyboard until something asks for one.
/// </summary>
/// <remarks>
/// <para>
/// Moving focus into the screen just navigated to is a courtesy with a keyboard and an ambush
/// without one: on a phone, focusing the first text box opens the system keyboard over the content
/// the user navigated to read.
/// </para>
/// <para>
/// The platform decides it by default - Avalonia's single-view lifetime means no windows and, in
/// practice, no keyboard - and that lifetime cannot be constructed outside Avalonia, so what is
/// asserted here is the seam an application uses to say the same thing for itself.
/// </para>
/// </remarks>
public class SingleViewFocusTests
{
    private sealed class Inline : IUIDispatcher
    {
        public bool CheckAccess() => true;
        public void Post(Action action) => action();
        public void Invoke(Action action) => action();
        public T Invoke<T>(Func<T> callback) => callback();
        public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
        public Task<T> InvokeAsync<T>(Func<Task<T>> callback) => callback();
    }

    /// <summary>A host that was never told either way, which is the only case the platform decides.</summary>
    private static (NavigationService Navigation, Window Window, NavigationHost Host) Attach()
    {
        StubServices services = new();
        NavigationService navigation = new(services, new Inline());

        TypeRegistry registry = new([Assembly.GetExecutingAssembly()]);
        registry.Initialize();

        NavigationHost host = new()
        {
            Service = navigation,
            Locator = new ViewLocator(services, registry)
        };

        Window window = new() { Content = host, Width = 400, Height = 300 };
        window.Show();
        window.Activate();
        Pump();

        return (navigation, window, host);
    }

    private static void Pump()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }
    }

    private static object? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    /// <summary>Runs a test with the application's answer set, and puts it back afterwards.</summary>
    private static async Task WithApplicationMovesFocus(bool value, Func<Task> test)
    {
        Application application = Application.Current!;
        bool had = application.IsSet(NavigationHost.ApplicationMovesFocusProperty);
        bool previous = NavigationHost.GetApplicationMovesFocus(application);

        NavigationHost.SetApplicationMovesFocus(application, value);

        try
        {
            await test();
        }
        finally
        {
            if (had) NavigationHost.SetApplicationMovesFocus(application, previous);
            else application.ClearValue(NavigationHost.ApplicationMovesFocusProperty);
        }
    }

    [AvaloniaFact]
    public Task WhereTheShellSaysItHasNoKeyboard_ArrivingDoesNotTakeFocus() =>
        WithApplicationMovesFocus(false, async () =>
        {
            (NavigationService navigation, Window window, _) = Attach();

            await navigation.NavigateToAsync<NavigationFocusTests.TwoButtonsViewModel>();
            Pump();

            Assert.IsNotType<Button>(Focused(window));
            window.Close();
        });

    [AvaloniaFact]
    public Task WhereItSaysItHasOne_ArrivingTakesFocus() =>
        WithApplicationMovesFocus(true, async () =>
        {
            (NavigationService navigation, Window window, _) = Attach();

            await navigation.NavigateToAsync<NavigationFocusTests.TwoButtonsViewModel>();
            Pump();

            Button focused = Assert.IsType<Button>(Focused(window));
            Assert.Equal("first", focused.Content);
            window.Close();
        });

    /// <summary>A host that was told wins over whatever the application answers for the rest.</summary>
    [AvaloniaFact]
    public Task AHostThatWasToldExplicitly_IsObeyedAnyway() =>
        WithApplicationMovesFocus(false, async () =>
        {
            (NavigationService navigation, Window window, NavigationHost host) = Attach();

            host.MovesFocusOnNavigation = true;

            await navigation.NavigateToAsync<NavigationFocusTests.TwoButtonsViewModel>();
            Pump();

            Assert.IsType<Button>(Focused(window));
            window.Close();
        });

    /// <summary>
    /// With nothing said anywhere, a desktop test session keeps the behaviour it always had.
    /// </summary>
    [AvaloniaFact]
    public async Task WithNothingSaid_TheDesktopDefaultIsUnchanged()
    {
        (NavigationService navigation, Window window, _) = Attach();

        await navigation.NavigateToAsync<NavigationFocusTests.TwoButtonsViewModel>();
        Pump();

        Assert.IsType<Button>(Focused(window));
        window.Close();
    }
}
