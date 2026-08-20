namespace CdCSharp.Pangea.Core.Abstractions;

/// <summary>Whether the machine currently has a network, and when that changes.</summary>
/// <remarks>
/// <para>
/// What this answers is "is it worth trying", not "will it work". A phone on a hotel wifi with a
/// captive portal is connected by every measure the operating system has and can reach nothing.
/// Requests still have to handle their own failures; this is what stops an application making them
/// pointlessly, and what tells an outbox that now is a good moment to drain.
/// </para>
/// <para>
/// The implementation the toolkit registers reads the operating system's network interfaces, which
/// works everywhere and is least accurate on a phone. A mobile head with the platform's own
/// connectivity API available registers its own over the top: the last registration wins.
/// </para>
/// </remarks>
public interface IConnectivity
{
    /// <summary>Whether the operating system reports a usable network.</summary>
    bool IsConnected { get; }

    /// <summary>Raised when the answer changes. Not guaranteed to be raised on the UI thread.</summary>
    event EventHandler<ConnectivityChangedEventArgs>? Changed;
}

/// <summary>What the network state changed to.</summary>
public sealed class ConnectivityChangedEventArgs(bool isConnected) : EventArgs
{
    /// <summary>What <see cref="IConnectivity.IsConnected"/> now reports.</summary>
    public bool IsConnected { get; } = isConnected;
}

/// <summary>Waiting for the network, for the work that has no reason to start without one.</summary>
public static class ConnectivityExtensions
{
    /// <summary>
    /// Completes as soon as there is a network, immediately when there already is one.
    /// </summary>
    /// <remarks>
    /// What an outbox waits on rather than polling. Cancel it to stop waiting - on the way out of a
    /// screen, or when the application is closing.
    /// </remarks>
    public static Task WaitForConnectionAsync(
        this IConnectivity connectivity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectivity);

        if (connectivity.IsConnected) return Task.CompletedTask;

        TaskCompletionSource waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnChanged(object? sender, ConnectivityChangedEventArgs e)
        {
            if (e.IsConnected) waiting.TrySetResult();
        }

        connectivity.Changed += OnChanged;

        CancellationTokenRegistration registration = cancellationToken.Register(() => waiting.TrySetCanceled(cancellationToken));

        // Re-read after subscribing: the network can arrive between the check above and the
        // subscription, and nothing would raise the event again to say so.
        if (connectivity.IsConnected) waiting.TrySetResult();

        return waiting.Task.ContinueWith(
            completed =>
            {
                connectivity.Changed -= OnChanged;
                registration.Dispose();
                return completed;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default).Unwrap();
    }
}
