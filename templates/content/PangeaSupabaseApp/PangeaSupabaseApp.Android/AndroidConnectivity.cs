using Android.Content;
using Android.Net;
using CdCSharp.Pangea.Core.Abstractions;

namespace PangeaSupabaseApp.Android;

/// <summary>
/// The network, as Android reports it.
/// </summary>
/// <remarks>
/// <para>
/// Better than reading the interfaces, which is what the toolkit's default does: this follows the
/// system's own idea of a validated connection, so a phone attached to a wifi that goes nowhere is
/// reported as disconnected rather than as connected.
/// </para>
/// <para>
/// Worked out from what the callbacks are handed, never by asking the manager again from inside
/// one. Android says that answer can be stale there, and it is worse than stale while the
/// application is held back by Doze or app standby: <c>ActiveNetwork</c> is null for a blocked
/// application. Asked that way, a phone left on the table reported no network on the first
/// capability change of the night - and nothing asked again when it was unblocked, because that
/// arrives through <see cref="ConnectivityManager.NetworkCallback.OnBlockedStatusChanged"/>. An
/// outbox waiting on this stayed full, with the network working, until the application was restarted.
/// </para>
/// </remarks>
public sealed class AndroidConnectivity : IConnectivity, IDisposable
{
    private readonly ConnectivityManager _manager;
    private readonly Callback _callback;
    private readonly object _gate = new();

    private Network? _network;
    private bool _usable;
    private bool _blocked;

    public AndroidConnectivity(Context context)
    {
        _manager = (ConnectivityManager)context.GetSystemService(Context.ConnectivityService)!;

        // A first answer for whoever asks before the callback's first round arrives; registering
        // it delivers the default network's whole state straight away, and that replaces this.
        if (_manager.ActiveNetwork is { } active)
        {
            _network = active;
            _usable = IsUsable(_manager.GetNetworkCapabilities(active));
        }

        IsConnected = _network is not null && _usable;

        _callback = new Callback(this);
        _manager.RegisterDefaultNetworkCallback(_callback);
    }

    public event EventHandler<ConnectivityChangedEventArgs>? Changed;

    public bool IsConnected { get; private set; }

    public void Dispose() => _manager.UnregisterNetworkCallback(_callback);

    private static bool IsUsable(NetworkCapabilities? capabilities) =>
        capabilities is not null
        && capabilities.HasCapability(NetCapability.Internet)
        && capabilities.HasCapability(NetCapability.Validated);

    private void OnAvailable(Network network)
    {
        lock (_gate)
        {
            if (!network.Equals(_network))
            {
                _network = network;

                // From Android 8 the capabilities follow at once and settle this. Before it they
                // may not, and this network's own - not the active one's - is the answer there is.
                _usable = IsUsable(_manager.GetNetworkCapabilities(network));
                _blocked = false;
            }
        }

        Publish();
    }

    private void OnCapabilitiesChanged(Network network, NetworkCapabilities capabilities)
    {
        lock (_gate)
        {
            // The default network's callback speaks only of the default network.
            _network = network;
            _usable = IsUsable(capabilities);
        }

        Publish();
    }

    private void OnBlockedStatusChanged(Network network, bool blocked)
    {
        lock (_gate)
        {
            _network = network;
            _blocked = blocked;
        }

        Publish();
    }

    private void OnLost(Network network)
    {
        lock (_gate)
        {
            if (!network.Equals(_network)) return;

            _network = null;
            _usable = false;
            _blocked = false;
        }

        Publish();
    }

    private void Publish()
    {
        bool connected;

        lock (_gate)
        {
            connected = _network is not null && _usable && !_blocked;

            // Only the transitions. Android raises its callbacks generously - every change of
            // signal strength is one - and an outbox waiting on this would drain on every one.
            if (connected == IsConnected) return;

            IsConnected = connected;
        }

        Changed?.Invoke(this, new ConnectivityChangedEventArgs(connected));
    }

    private sealed class Callback(AndroidConnectivity owner) : ConnectivityManager.NetworkCallback
    {
        public override void OnAvailable(Network network) => owner.OnAvailable(network);

        public override void OnLost(Network network) => owner.OnLost(network);

        public override void OnCapabilitiesChanged(Network network, NetworkCapabilities networkCapabilities) =>
            owner.OnCapabilitiesChanged(network, networkCapabilities);

        public override void OnBlockedStatusChanged(Network network, bool blocked) =>
            owner.OnBlockedStatusChanged(network, blocked);
    }
}
