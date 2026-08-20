using CdCSharp.Pangea.Core.Abstractions;
using Network;

namespace PangeaSupabaseApp.iOS;

/// <summary>
/// The network, as iOS reports it.
/// </summary>
/// <remarks>
/// <c>NWPathMonitor</c> is what Apple wants an application to use: it answers before a request has
/// to fail to find out, and it notices a change the moment the system does rather than when
/// something next asks.
/// </remarks>
public sealed class PathMonitorConnectivity : IConnectivity, IDisposable
{
    private readonly NWPathMonitor _monitor = new();

    public PathMonitorConnectivity()
    {
        _monitor.SnapshotHandler = OnPath;
        _monitor.SetQueue(CoreFoundation.DispatchQueue.DefaultGlobalQueue);
        _monitor.Start();
    }

    public event EventHandler<ConnectivityChangedEventArgs>? Changed;

    public bool IsConnected { get; private set; } = true;

    public void Dispose()
    {
        _monitor.Cancel();
        _monitor.Dispose();
    }

    private void OnPath(NWPath path)
    {
        bool connected = path.Status is NWPathStatus.Satisfied;

        // Only the transitions: the monitor reports a snapshot on every change to the path,
        // including ones that do not alter whether there is a network at all.
        if (connected == IsConnected) return;

        IsConnected = connected;
        Changed?.Invoke(this, new ConnectivityChangedEventArgs(connected));
    }
}
