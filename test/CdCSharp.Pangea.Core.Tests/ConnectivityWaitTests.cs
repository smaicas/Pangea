using CdCSharp.Pangea.Core.Abstractions;

namespace CdCSharp.Pangea.Core.Tests;

/// <summary>
/// Waiting for a network, which is what an outbox does instead of polling and instead of guessing
/// from the shape of the last exception.
/// </summary>
public class ConnectivityWaitTests
{
    private sealed class Fake : IConnectivity
    {
        public bool IsConnected { get; private set; }

        public event EventHandler<ConnectivityChangedEventArgs>? Changed;

        public bool HasWaiters => Changed is not null;

        public void Connect()
        {
            IsConnected = true;
            Changed?.Invoke(this, new ConnectivityChangedEventArgs(true));
        }
    }

    [Fact]
    public async Task WithANetworkAlready_TheWaitIsOver()
    {
        Fake connectivity = new();
        connectivity.Connect();

        await connectivity.WaitForConnectionAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WithoutOne_ItWaitsUntilThereIs()
    {
        Fake connectivity = new();

        Task waiting = connectivity.WaitForConnectionAsync(TestContext.Current.CancellationToken);

        Assert.False(waiting.IsCompleted);

        connectivity.Connect();

        await waiting;

        // Subscribed for the wait and unsubscribed after it: a waiter left behind is a leak in the
        // one place an application waits over and over.
        Assert.False(connectivity.HasWaiters);
    }

    [Fact]
    public async Task Cancelled_ItStopsWaitingAndLetsGo()
    {
        Fake connectivity = new();
        using CancellationTokenSource cancellation = new();

        Task waiting = connectivity.WaitForConnectionAsync(cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.False(connectivity.HasWaiters);
    }
}
