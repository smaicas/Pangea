using Android.Content;
using Android.Net;
using CdCSharp.Pangea.Core.Abstractions;

namespace PangeaMobileApp.Android;

/// <summary>
/// The network, as Android reports it.
/// </summary>
/// <remarks>
/// Better than reading the interfaces, which is what the toolkit's default does: this follows the
/// system's own idea of a validated connection, so a phone attached to a wifi that goes nowhere is
/// reported as disconnected rather than as connected.
/// </remarks>
public sealed class AndroidConnectivity : IConnectivity, IDisposable
{
    private readonly ConnectivityManager _manager;
    private readonly Callback _callback;

    public AndroidConnectivity(Context context)
    {
        _manager = (ConnectivityManager)context.GetSystemService(Context.ConnectivityService)!;
        _callback = new Callback(this);

        _manager.RegisterDefaultNetworkCallback(_callback);

        IsConnected = HasValidatedNetwork();
    }

    public event EventHandler<ConnectivityChangedEventArgs>? Changed;

    public bool IsConnected { get; private set; }

    public void Dispose() => _manager.UnregisterNetworkCallback(_callback);

    private bool HasValidatedNetwork() =>
        _manager.GetNetworkCapabilities(_manager.ActiveNetwork) is { } capabilities
        && capabilities.HasCapability(NetCapability.Internet)
        && capabilities.HasCapability(NetCapability.Validated);

    private void Update()
    {
        bool connected = HasValidatedNetwork();

        // Only the transitions. Android raises its callbacks generously, and an outbox waiting on
        // this would drain on every one of them.
        if (connected == IsConnected) return;

        IsConnected = connected;
        Changed?.Invoke(this, new ConnectivityChangedEventArgs(connected));
    }

    private sealed class Callback(AndroidConnectivity owner) : ConnectivityManager.NetworkCallback
    {
        public override void OnAvailable(Network network) => owner.Update();

        public override void OnLost(Network network) => owner.Update();

        public override void OnCapabilitiesChanged(Network network, NetworkCapabilities capabilities) =>
            owner.Update();
    }
}
