namespace CdCSharp.Pangea.Apps.Testing;

/// <summary>
/// The host context, connected and disconnected on cue.
/// </summary>
/// <remarks>
/// Not hosted to start with, which is how an application starts. <see cref="Connect"/> and
/// <see cref="Disconnect"/> raise <see cref="Changed"/> as the real one does when a host appears
/// or goes away. Independent of <see cref="FakeAppBus"/>: a test about both sets both.
/// </remarks>
public sealed class FakeAppHost : IAppHostContext
{
    /// <param name="identity">The application's manifest; a test application when null.</param>
    public FakeAppHost(AppIdentity? identity = null) => Identity = identity ?? new AppIdentity("test-app", "Test app");

    public AppIdentity Identity { get; }

    /// <summary>Whether a host started the process. Settable: it is decided once, at start.</summary>
    public bool LaunchedByHost { get; set; }

    public bool IsHosted { get; private set; }

    public string? HostName { get; private set; }

    public AppEnvironment? Environment { get; private set; }

    public event EventHandler? Changed;

    /// <summary>A host connects, deciding <paramref name="environment"/> for the application.</summary>
    public FakeAppHost Connect(string hostName = "Omni", AppEnvironment? environment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);

        IsHosted = true;
        HostName = hostName;
        Environment = environment ?? new AppEnvironment { HostName = hostName };
        Changed?.Invoke(this, EventArgs.Empty);
        return this;
    }

    /// <summary>The host goes away.</summary>
    public FakeAppHost Disconnect()
    {
        IsHosted = false;
        HostName = null;
        Environment = null;
        Changed?.Invoke(this, EventArgs.Empty);
        return this;
    }
}
