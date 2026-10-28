using CdCSharp.Pangea.Apps.Wire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.IO.Pipes;

namespace CdCSharp.Pangea.Apps.Tests;

/// <summary>What an application has going on, as the host sees it and stops it, over real pipes.</summary>
public sealed class ResourceTests : IAsyncLifetime
{
    private readonly BusHarness _bus = new();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => _bus.DisposeAsync();

    private static void Reporting(IServiceCollection services) => services.AddSingleton<AppActivity>().AddSingleton<AppResources>();

    [Fact]
    public async Task What_an_application_has_going_on_reaches_the_host_and_follows_its_changes()
    {
        AppBusClient app = await _bus.ConnectAsync("mia", Reporting);
        AppResources resources = Resources(app);

        IAppResource model = resources.Add(AppResourceKind.Model, "GLM-4.7-Flash", "CUDA0", processId: 4242);
        await BusHarness.Until(() => _bus.Broker.ResourcesOf("mia").Count == 1);

        model.Detail = "CUDA0 · answering";
        await BusHarness.Until(() => _bus.Broker.ResourcesOf("mia").SingleOrDefault()?.Detail == "CUDA0 · answering");

        AppResourceInfo seen = _bus.Broker.ResourcesOf("mia")[0];
        Assert.Equal((AppResourceKind.Model, "GLM-4.7-Flash", 4242, false), (seen.Kind, seen.Title, seen.ProcessId, seen.CanStop));
        Assert.True(_bus.Broker.Apps.Single().ReportsResources);

        model.Dispose();
        await BusHarness.Until(() => _bus.Broker.ResourcesOf("mia").Count == 0);
    }

    [Fact]
    public async Task The_host_stops_a_resource_through_the_application_which_then_leaves_the_list()
    {
        AppBusClient app = await _bus.ConnectAsync("mia", Reporting);
        bool unloaded = false;
        Resources(app).Add(AppResourceKind.Model, "GLM-4.7-Flash", stop: _ =>
        {
            unloaded = true;
            return Task.CompletedTask;
        });
        await BusHarness.Until(() => _bus.Broker.ResourcesOf("mia").Count == 1);

        bool? stopped = await _bus.Broker.StopResourceAsync("mia", _bus.Broker.ResourcesOf("mia")[0].Id, TimeSpan.FromSeconds(10));

        Assert.True(stopped);
        Assert.True(unloaded);
        await BusHarness.Until(() => _bus.Broker.ResourcesOf("mia").Count == 0);
    }

    [Fact]
    public async Task Work_in_progress_is_listed_as_tasks_that_stop_when_they_can()
    {
        AppBusClient app = await _bus.ConnectAsync("yutuf", Reporting);
        AppActivity activity = _bus.ServicesOf(app).GetRequiredService<AppActivity>();
        using CancellationTokenSource download = new();

        using IDisposable plain = activity.Begin("Saving the library");
        using IDisposable stoppable = activity.Begin("Downloading «song»", _ => download.CancelAsync());
        await BusHarness.Until(() => _bus.Broker.ResourcesOf("yutuf").Count == 2);

        AppResourceInfo[] tasks = [.. _bus.Broker.ResourcesOf("yutuf")];
        Assert.All(tasks, task => Assert.Equal(AppResourceKind.Task, task.Kind));
        Assert.Equal([false, true], tasks.Select(task => task.CanStop));

        Assert.False(await _bus.Broker.StopResourceAsync("yutuf", tasks[0].Id, TimeSpan.FromSeconds(10)));
        Assert.True(await _bus.Broker.StopResourceAsync("yutuf", tasks[1].Id, TimeSpan.FromSeconds(10)));
        Assert.True(download.IsCancellationRequested);
    }

    [Fact]
    public async Task A_stop_that_fails_reaches_the_host_with_the_applications_reason()
    {
        AppBusClient app = await _bus.ConnectAsync("diskord", Reporting);
        Resources(app).Add(AppResourceKind.Service, "Bot Bender", stop: _ => throw new IOException("The host is not answering."));
        await BusHarness.Until(() => _bus.Broker.ResourcesOf("diskord").Count == 1);

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _bus.Broker.StopResourceAsync("diskord", _bus.Broker.ResourcesOf("diskord")[0].Id, TimeSpan.FromSeconds(10)));

        Assert.Contains("not answering", ex.Message);
    }

    [Fact]
    public async Task An_application_that_does_not_report_is_never_asked_to_stop_anything()
    {
        await _bus.ConnectAsync("old");

        Assert.False(_bus.Broker.Apps.Single().ReportsResources);
        Assert.Empty(_bus.Broker.ResourcesOf("old"));
        Assert.Null(await _bus.Broker.StopResourceAsync("old", "res-1", TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task When_the_application_goes_away_so_does_what_it_had_going_on()
    {
        AppBusClient app = await _bus.ConnectAsync("mia", Reporting);
        Resources(app).Add(AppResourceKind.Process, "llama-server");
        await BusHarness.Until(() => _bus.Broker.ResourcesOf("mia").Count == 1);
        List<string> changed = [];
        _bus.Broker.ResourcesChanged += (_, appId) => changed.Add(appId);

        await app.DisposeAsync();

        await BusHarness.Until(() => !_bus.Broker.IsConnected("mia"));
        Assert.Empty(_bus.Broker.ResourcesOf("mia"));
        Assert.Contains("mia", changed);
    }

    [Fact]
    public async Task A_host_that_does_not_announce_resources_is_never_sent_them()
    {
        // A host of an earlier version: it does not know the frame, and would drop the connection on reading it.
        string pipe = "pangea-old-host-" + Guid.NewGuid().ToString("N");
        await using NamedPipeServerStream server = new(pipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

        ServiceCollection collection = new();
        Reporting(collection);
        await using ServiceProvider provider = collection.BuildServiceProvider();
        await using AppBusClient app = new(new AppIdentity("mia", "mIA"), provider,
            Options.Create(new AppsOptions { PipeName = pipe, RetryInterval = TimeSpan.FromMilliseconds(100) }), [], logger: null, includeGenerated: false);

        app.Start();
        await server.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        await using FrameStream frames = new(server);

        Frame? hello = await frames.ReadAsync(TestContext.Current.CancellationToken);
        await frames.WriteAsync(Frame.Of(FrameKind.Welcome, new HostWelcome("Old host", new AppEnvironment())), TestContext.Current.CancellationToken);
        await BusHarness.Until(() => app.IsConnected);

        provider.GetRequiredService<AppResources>().Add(AppResourceKind.Model, "GLM-4.7-Flash");

        using CancellationTokenSource quiet = new(TimeSpan.FromMilliseconds(600));
        List<Frame> received = [];

        try
        {
            while (await frames.ReadAsync(quiet.Token) is { } frame) received.Add(frame);
        }
        catch (OperationCanceledException)
        {
            // Nothing more came, as it should.
        }

        Assert.Equal([AppBusFeatures.Resources], hello!.Read<AppHello>()!.Features);
        Assert.DoesNotContain(received, frame => frame.Kind == FrameKind.Resources);
    }

    private AppResources Resources(AppBusClient app) => _bus.ServicesOf(app).GetRequiredService<AppResources>();
}
