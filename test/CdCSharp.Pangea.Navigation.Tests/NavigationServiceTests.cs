using System.Collections.Concurrent;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Navigation.Abstractions;
using CdCSharp.Pangea.Navigation.Tests.Infrastructure;
using System.ComponentModel;

namespace CdCSharp.Pangea.Navigation.Tests;

/// <summary>
/// The stack, the lifecycle hooks and the order they run in. The hooks existed on every view model
/// long before anything called them, so what is asserted here is that they now fire, once, in the
/// order a screen can rely on.
/// </summary>
public class NavigationServiceTests
{
    private static NavigationService Create(out StubServices services)
    {
        services = new StubServices();
        return new NavigationService(services, new ImmediateDispatcher());
    }

    private static NavigationService Create() => Create(out _);

    [Fact]
    public async Task NavigatingWithARequest_HandsItToTheDestinationTyped()
    {
        NavigationService navigation = Create(out StubServices services);
        Guid id = Guid.NewGuid();

        Assert.True(await navigation.NavigateToAsync(new ShowOrder(id)));

        OrderViewModel order = Assert.IsType<OrderViewModel>(navigation.CurrentViewModel);
        Assert.Equal(id, order.ReceivedId);
        Assert.Equal(["arrived-with-request"], order.Calls);
    }

    [Fact]
    public async Task NavigatingWithoutARequest_UsesTheParameterlessHook()
    {
        NavigationService navigation = Create();

        Assert.True(await navigation.NavigateToAsync<OrderViewModel>());

        OrderViewModel order = Assert.IsType<OrderViewModel>(navigation.CurrentViewModel);
        Assert.Null(order.ReceivedId);
        Assert.Equal(["arrived"], order.Calls);
    }

    [Fact]
    public async Task AViewModelThatIgnoresNavigation_IsStillAValidDestination()
    {
        NavigationService navigation = Create();

        Assert.True(await navigation.NavigateToAsync<PlainViewModel>());

        Assert.IsType<PlainViewModel>(navigation.CurrentViewModel);
    }

    [Fact]
    public async Task LeavingAScreen_AsksThenTellsIt()
    {
        NavigationService navigation = Create();

        await navigation.NavigateToAsync(new ShowOrder(Guid.NewGuid()));
        OrderViewModel order = (OrderViewModel)navigation.CurrentViewModel!;
        order.Calls.Clear();

        await navigation.NavigateToAsync(new ShowReport("q1"));

        Assert.Equal(["asked-to-leave", "left"], order.Calls);
    }

    [Fact]
    public async Task AScreenThatRefusesToLeave_StopsTheNavigation()
    {
        NavigationService navigation = Create();

        await navigation.NavigateToAsync(new ShowOrder(Guid.NewGuid()));
        OrderViewModel order = (OrderViewModel)navigation.CurrentViewModel!;
        order.AllowLeaving = false;
        order.Calls.Clear();

        Assert.False(await navigation.NavigateToAsync(new ShowReport("q1")));

        Assert.Same(order, navigation.CurrentViewModel);
        Assert.Equal(["asked-to-leave"], order.Calls);
        Assert.False(navigation.CanGoBack);
    }

    [Fact]
    public async Task GoingBack_ReturnsTheSameInstance()
    {
        NavigationService navigation = Create();

        await navigation.NavigateToAsync(new ShowOrder(Guid.NewGuid()));
        object first = navigation.CurrentViewModel!;

        await navigation.NavigateToAsync(new ShowReport("q1"));
        Assert.True(navigation.CanGoBack);

        Assert.True(await navigation.GoBackAsync());

        Assert.Same(first, navigation.CurrentViewModel);
        Assert.False(navigation.CanGoBack);
    }

    /// <summary>
    /// Coming back is not arriving with data: re-running the request hook would reload the screen
    /// against whatever the request said when it was first opened.
    /// </summary>
    [Fact]
    public async Task GoingBack_DoesNotReplayTheRequest()
    {
        NavigationService navigation = Create();

        await navigation.NavigateToAsync(new ShowOrder(Guid.NewGuid()));
        OrderViewModel order = (OrderViewModel)navigation.CurrentViewModel!;

        await navigation.NavigateToAsync(new ShowReport("q1"));
        order.Calls.Clear();

        await navigation.GoBackAsync();

        Assert.Equal(["arrived"], order.Calls);
    }

    [Fact]
    public async Task GoingBackWithNoHistory_DoesNothing()
    {
        NavigationService navigation = Create();

        Assert.False(await navigation.GoBackAsync());
        Assert.Null(navigation.CurrentViewModel);
    }

    [Fact]
    public async Task ClearHistory_LeavesTheScreenAloneAndDropsTheStack()
    {
        NavigationService navigation = Create();

        await navigation.NavigateToAsync(new ShowOrder(Guid.NewGuid()));
        await navigation.NavigateToAsync(new ShowReport("q1"));

        object current = navigation.CurrentViewModel!;
        navigation.ClearHistory();

        Assert.Same(current, navigation.CurrentViewModel);
        Assert.False(navigation.CanGoBack);
    }

    [Fact]
    public async Task BothCurrentViewModelAndCanGoBack_AreNotified()
    {
        NavigationService navigation = Create();
        List<string> changed = [];
        ((INotifyPropertyChanged)navigation).PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        await navigation.NavigateToAsync(new ShowOrder(Guid.NewGuid()));
        await navigation.NavigateToAsync(new ShowReport("q1"));

        Assert.Contains(nameof(INavigationService.CurrentViewModel), changed);
        Assert.Contains(nameof(INavigationService.CanGoBack), changed);
    }

    /// <summary>
    /// Navigation ends in a property change a control is bound to, and its hooks are where a screen
    /// loads what it displays. It belongs on the UI thread whichever thread started it - the same
    /// mistake that made the window manager return before it had closed anything.
    /// </summary>
    /// <remarks>
    /// The dispatcher is the test's own rather than Avalonia's: whether the headless dispatcher has
    /// thread affinity varies by platform, and what is being asserted here is that navigation goes
    /// through <see cref="IUIDispatcher"/> at all instead of running wherever it was called.
    /// </remarks>
    [Fact]
    public async Task NavigatingFromABackgroundThread_MarshalsThroughTheDispatcher()
    {
        QueueingDispatcher dispatcher = new();
        NavigationService navigation = new(new StubServices(), dispatcher);

        // A dedicated thread, not the pool: a test body is itself pool work, and awaiting hands
        // its thread back, so Task.Run can legitimately reuse the very thread that owns the
        // dispatcher - and then nothing marshals and the test measures nothing.
        Task<bool> navigating = null!;
        Thread worker = new(() => navigating = navigation.NavigateToAsync<ThreadRecordingViewModel>());
        worker.Start();
        worker.Join();

        // No timing involved: the work is queued and only this thread can run it.
        Assert.Equal(1, dispatcher.QueuedCount);
        Assert.False(navigating.IsCompleted);

        int drainingThread = Environment.CurrentManagedThreadId;
        dispatcher.Drain();

        Assert.True(await navigating);

        ThreadRecordingViewModel arrived = Assert.IsType<ThreadRecordingViewModel>(navigation.CurrentViewModel);
        Assert.Equal(drainingThread, arrived.HookThreadId);
    }

    /// <summary>Runs everything inline, so the stack can be asserted without a UI thread.</summary>
    private sealed class ImmediateDispatcher : IUIDispatcher
    {
        public bool CheckAccess() => true;

        public void Post(Action action) => action();

        public void Invoke(Action action) => action();

        public Task<T> InvokeAsync<T>(Func<Task<T>> callback) => callback();
    }

    /// <summary>
    /// A dispatcher owned by the thread that constructs it, draining only when told to. Standing in
    /// for the real one makes the assertion about our marshalling rather than about Avalonia's.
    /// </summary>
    private sealed class QueueingDispatcher : IUIDispatcher
    {
        private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
        private readonly ConcurrentQueue<Action> _queued = new();

        /// <summary>How much work was marshalled rather than run where it was called.</summary>
        public int QueuedCount { get; private set; }

        public bool CheckAccess() => Environment.CurrentManagedThreadId == _ownerThreadId;

        public void Post(Action action) => _queued.Enqueue(action);

        public void Invoke(Action action)
        {
            if (CheckAccess()) action();
            else _queued.Enqueue(action);
        }

        public Task<T> InvokeAsync<T>(Func<Task<T>> callback)
        {
            if (CheckAccess()) return callback();

            TaskCompletionSource<T> completion = new();
            QueuedCount++;

            _queued.Enqueue(async void () =>
            {
                try
                {
                    completion.SetResult(await callback());
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            });

            return completion.Task;
        }

        public void Drain()
        {
            while (_queued.TryDequeue(out Action? queued))
            {
                queued();
            }
        }
    }
}
