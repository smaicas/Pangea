using CdCSharp.Pangea.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace CdCSharp.Pangea.Apps.Testing;

public static class PangeaTestServicesAppsExtensions
{
    /// <summary>
    /// Adds what an application in a host asks its container for: <see cref="IAppBus"/> and
    /// <see cref="IAppHostContext"/> as doubles, and the real <see cref="AppActivity"/>,
    /// <see cref="AppInteractions"/>, <see cref="AppRequestFolders"/> and <see cref="AppIdentity"/>.
    /// </summary>
    /// <param name="services">The test container.</param>
    /// <param name="bus">The bus to register; a new, connected one when null. Also registered as itself.</param>
    /// <param name="host">The host to register; a new, unhosted one when null. Also registered as itself.</param>
    /// <remarks>
    /// The request folders live in a folder of their own among the temporary files, new for every
    /// call, and are created only when a folder for a request is.
    /// </remarks>
    public static PangeaTestServices AddApps(this PangeaTestServices services, FakeAppBus? bus = null, FakeAppHost? host = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        bus ??= new FakeAppBus();
        host ??= new FakeAppHost();

        AppActivity activity = new();
        string requests = Path.Combine(Path.GetTempPath(), "pangea-tests", host.Identity.Id, Guid.NewGuid().ToString("N"));

        return services
            .Add<IAppBus>(bus)
            .Add(bus)
            .Add<IAppHostContext>(host)
            .Add(host)
            .Add(host.Identity)
            .Add(activity)
            .Add(new AppInteractions(activity, services))
            .Add(new AppRequestFolders(requests, NullLogger<AppRequestFolders>.Instance));
    }

    /// <summary>The bus <see cref="AddApps"/> registered.</summary>
    /// <exception cref="InvalidOperationException"><see cref="AddApps"/> was not called.</exception>
    public static FakeAppBus AppBus(this PangeaTestServices services) =>
        services.GetService(typeof(FakeAppBus)) as FakeAppBus
        ?? throw new InvalidOperationException("No FakeAppBus registered: call AddApps first.");

    /// <summary>The host <see cref="AddApps"/> registered.</summary>
    /// <exception cref="InvalidOperationException"><see cref="AddApps"/> was not called.</exception>
    public static FakeAppHost AppHost(this PangeaTestServices services) =>
        services.GetService(typeof(FakeAppHost)) as FakeAppHost
        ?? throw new InvalidOperationException("No FakeAppHost registered: call AddApps first.");
}
