using CdCSharp.Pangea.Apps.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Apps.Tests;

/// <summary>
/// A broker on a pipe of its own and as many applications as a test wants, each a real client over
/// a real named pipe - the same path a message takes between two processes.
/// </summary>
internal sealed class BusHarness : IAsyncDisposable
{
    private readonly List<AppBusClient> _clients = [];
    private readonly List<ServiceProvider> _providers = [];

    public BusHarness(Action<AppBrokerOptions>? configure = null)
    {
        PipeName = "pangea-apps-test-" + Guid.NewGuid().ToString("N");

        AppBrokerOptions options = new() { HostName = "Test host", PipeName = PipeName };
        configure?.Invoke(options);

        Broker = new AppBroker(options);
        Broker.Start();
    }

    public string PipeName { get; }

    public AppBroker Broker { get; }

    /// <summary>An application that is not connected yet: call <see cref="AppBusClient.Start"/>, or use <see cref="ConnectAsync"/>.</summary>
    public AppBusClient Create(string appId, Action<IServiceCollection>? services = null, bool includeGenerated = false)
    {
        ServiceCollection collection = new();
        services?.Invoke(collection);

        ServiceProvider provider = collection.BuildServiceProvider();
        _providers.Add(provider);

        AppBusClient client = new(
            new AppIdentity(appId, appId.ToUpperInvariant()) { Version = "1.0.0", Commit = "abcdef1234567" },
            provider,
            Options.Create(new AppsOptions { PipeName = PipeName, RetryInterval = TimeSpan.FromMilliseconds(100) }),
            provider.GetServices<AppHandlerEntry>(),
            logger: null,
            includeGenerated);

        _clients.Add(client);
        return client;
    }

    /// <summary>An application, connected and greeted.</summary>
    public async Task<AppBusClient> ConnectAsync(string appId, Action<IServiceCollection>? services = null, bool includeGenerated = false)
    {
        AppBusClient client = Create(appId, services, includeGenerated);
        client.Start();

        await Until(() => client.IsConnected && Broker.IsConnected(appId));
        return client;
    }

    /// <summary>Waits for something that happens on another thread, failing the test after a while.</summary>
    public static async Task Until(Func<bool> condition, int timeoutMilliseconds = 10_000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);

        while (!condition())
        {
            if (DateTime.UtcNow > deadline) Assert.Fail("Timed out waiting for the bus.");

            await Task.Delay(20);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (AppBusClient client in _clients) await client.DisposeAsync();

        await Broker.DisposeAsync();

        foreach (ServiceProvider provider in _providers) await provider.DisposeAsync();
    }
}
