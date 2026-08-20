using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;

namespace CdCSharp.Pangea.Services;

/// <summary>
/// Connectivity as the operating system's network interfaces report it.
/// </summary>
/// <remarks>
/// <para>
/// The answer every platform can give without any platform code, which is why it is the default and
/// not the last word. It is at its weakest on a phone, where the interface list says a network
/// exists long before it carries anything and keeps saying so through a tunnel. A head with the
/// platform's own API - Android's <c>ConnectivityManager</c>, iOS's <c>NWPathMonitor</c> - should
/// register its own implementation; the last registration wins.
/// </para>
/// <para>
/// A failure to probe is reported as connected. Refusing to try because the check itself broke is
/// the worse answer: the request that follows either works or fails with a reason.
/// </para>
/// </remarks>
internal sealed class NetworkConnectivity : IConnectivity, IDisposable
{
    private readonly ILogger<NetworkConnectivity> _logger;
    private volatile bool _connected;
    private bool _disposed;

    public NetworkConnectivity(ILogger<NetworkConnectivity> logger)
    {
        _logger = logger;
        _connected = Probe();

        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
    }

    public bool IsConnected => _connected;

    public event EventHandler<ConnectivityChangedEventArgs>? Changed;

    private void OnNetworkChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        bool connected = Probe();

        if (connected == _connected) return;

        _connected = connected;

        _logger.LogInformation("The network is now {State}", connected ? "available" : "unavailable");

        Changed?.Invoke(this, new ConnectivityChangedEventArgs(connected));
    }

    private bool Probe()
    {
        try
        {
            return NetworkInterface.GetIsNetworkAvailable();
        }
        catch (NetworkInformationException ex)
        {
            _logger.LogDebug(ex, "The network state could not be read; assuming there is one");
            return true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        // Static events: without this the application keeps this alive, and every one ever built.
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
    }
}
