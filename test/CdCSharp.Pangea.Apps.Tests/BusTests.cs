using CdCSharp.Pangea.Apps.Hosting;
using CdCSharp.Pangea.Apps.Wire;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Apps.Tests;

/// <summary>
/// Two applications and a broker, over real named pipes: what has to hold for one application to
/// rely on another it does not know.
/// </summary>
public class BusTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_request_reaches_the_application_that_handles_it_and_the_answer_comes_back()
    {
        await using BusHarness bus = new();
        await bus.ConnectAsync("echoer", services => services.AddAppRequestHandler<Echo, EchoReply, EchoHandler>());
        AppBusClient sender = await bus.ConnectAsync("sender");

        EchoReply reply = await sender.SendAsync(new Echo("ab") { Times = 3 }, Token);

        Assert.Equal("ababab", reply.Text);
        Assert.Equal("sender", reply.Caller);
    }

    [Fact]
    public async Task Without_a_host_a_request_says_so_instead_of_waiting()
    {
        await using BusHarness bus = new();
        AppBusClient lonely = bus.Create("lonely");

        await Assert.ThrowsAsync<AppBusUnavailableException>(() => lonely.SendAsync(new Echo("x"), Token));
        Assert.False(await lonely.CanSendAsync<Echo>(Token));
    }

    [Fact]
    public async Task A_message_nobody_handles_fails_as_not_found()
    {
        await using BusHarness bus = new();
        AppBusClient sender = await bus.ConnectAsync("sender");

        AppHandlerNotFoundException ex = await Assert.ThrowsAsync<AppHandlerNotFoundException>(() => sender.SendAsync(new Nobody(), Token));

        Assert.Equal("tests.nobody", ex.MessageId);
        Assert.False(await sender.CanSendAsync<Nobody>(Token));
    }

    [Fact]
    public async Task A_handler_that_throws_reaches_the_sender_with_its_message()
    {
        await using BusHarness bus = new();
        await bus.ConnectAsync("failer", services => services.AddAppRequestHandler<Fail, bool, FailHandler>());
        AppBusClient sender = await bus.ConnectAsync("sender");

        AppRequestFailedException ex = await Assert.ThrowsAsync<AppRequestFailedException>(() => sender.SendAsync(new Fail("No queda disco."), Token));

        Assert.Equal("No queda disco.", ex.Message);
        Assert.Equal(typeof(InvalidOperationException).FullName, ex.RemoteType);
    }

    [Fact]
    public async Task Cancelling_a_request_cancels_the_handler_in_the_other_application()
    {
        WaitProbe probe = new();
        await using BusHarness bus = new();
        await bus.ConnectAsync("waiter", services => services
            .AddSingleton(probe)
            .AddAppRequestHandler<WaitForever, bool, WaitForeverHandler>());
        AppBusClient sender = await bus.ConnectAsync("sender");

        using CancellationTokenSource cancellation = new();
        Task<bool> request = sender.SendAsync(new WaitForever(), cancellation.Token);

        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        await probe.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
    }

    [Fact]
    public async Task When_the_handling_application_goes_away_the_sender_is_told()
    {
        WaitProbe probe = new();
        await using BusHarness bus = new();
        AppBusClient waiter = await bus.ConnectAsync("waiter", services => services
            .AddSingleton(probe)
            .AddAppRequestHandler<WaitForever, bool, WaitForeverHandler>());
        AppBusClient sender = await bus.ConnectAsync("sender");

        Task<bool> request = sender.SendAsync(new WaitForever(), Token);
        await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);

        await waiter.DisposeAsync();

        AppRequestFailedException ex = await Assert.ThrowsAsync<AppRequestFailedException>(() => request.WaitAsync(TimeSpan.FromSeconds(10), Token));
        Assert.Contains("WAITER", ex.Message);
    }

    [Fact]
    public async Task An_event_reaches_every_subscriber_but_not_its_publisher()
    {
        await using BusHarness bus = new();
        AppBusClient publisher = await bus.ConnectAsync("publisher");
        AppBusClient first = await bus.ConnectAsync("first");
        AppBusClient second = await bus.ConnectAsync("second");

        List<string> received = [];
        using IDisposable a = first.Subscribe<Happened>(e => { lock (received) received.Add("first:" + e.What); });
        using IDisposable b = second.Subscribe<Happened>(e => { lock (received) received.Add("second:" + e.What); });
        using IDisposable own = publisher.Subscribe<Happened>(e => { lock (received) received.Add("publisher:" + e.What); });

        // Subscriptions travel to the broker on their own; give them the moment they need.
        await Task.Delay(200, Token);
        await publisher.PublishAsync(new Happened("descargado"), Token);

        await BusHarness.Until(() => { lock (received) return received.Count >= 2; });
        await Task.Delay(100, Token);

        lock (received) Assert.Equal(["first:descargado", "second:descargado"], received.Order());
    }

    [Fact]
    public async Task Subscriptions_survive_the_host_restarting()
    {
        List<string> received = [];
        await using BusHarness bus = new();

        AppBusClient listener = await bus.ConnectAsync("listener");
        listener.Subscribe<Happened>(e => { lock (received) received.Add(e.What); });

        // The host goes away and a new one comes up on the same pipe, as when Omni is restarted.
        await bus.Broker.DisposeAsync();
        await BusHarness.Until(() => !listener.IsConnected);

        await using AppBroker restarted = new(new AppBrokerOptions { PipeName = bus.PipeName });
        restarted.Start();
        await BusHarness.Until(() => listener.IsConnected && restarted.IsConnected("listener"));

        AppBusClient publisher = bus.Create("publisher");
        publisher.Start();
        await BusHarness.Until(() => publisher.IsConnected && restarted.IsConnected("publisher"));
        await Task.Delay(200, Token);

        await publisher.PublishAsync(new Happened("otra vez"), Token);
        await BusHarness.Until(() => { lock (received) return received.Count == 1; });
    }

    [Fact]
    public async Task A_request_nobody_running_handles_starts_the_application_that_does()
    {
        BusHarness? bus = null;
        List<string> launched = [];

        bus = new BusHarness(options =>
        {
            options.CanLaunch = message => message == "tests.echo";
            options.LaunchHandler = (message, _) =>
            {
                launched.Add(message);
                bus!.Create("echoer", services => services.AddAppRequestHandler<Echo, EchoReply, EchoHandler>()).Start();
                return Task.FromResult(true);
            };
        });

        await using (bus)
        {
            AppBusClient sender = await bus.ConnectAsync("sender");

            Assert.True(await sender.CanSendAsync<Echo>(Token));

            EchoReply reply = await sender.SendAsync(new Echo("hola"), Token);

            Assert.Equal("hola", reply.Text);
            Assert.Equal(["tests.echo"], launched);
        }
    }

    [Fact]
    public async Task The_most_recently_connected_handler_wins_so_a_copy_under_a_debugger_takes_over()
    {
        await using BusHarness bus = new();
        await bus.ConnectAsync("installed", services => services.AddAppRequestHandler<Echo, EchoReply, EchoHandler>());
        await Task.Delay(50, Token);
        await bus.ConnectAsync("debugged", services => services.AddAppRequestHandler<Echo, EchoReply, EchoHandler>());
        AppBusClient sender = await bus.ConnectAsync("sender");

        List<BusTrace> traces = [];
        bus.Broker.Traced += (_, trace) => { lock (traces) traces.Add(trace); };

        await sender.SendAsync(new Echo("x"), Token);

        lock (traces) Assert.Contains(traces, trace => trace.Kind == FrameKind.Request && trace.To == "debugged");
    }

    [Fact]
    public async Task The_environment_reaches_applications_on_connection_and_when_it_changes()
    {
        await using BusHarness bus = new(options => options.Environment = new AppEnvironment { Culture = "es-ES" });
        AppBusClient app = await bus.ConnectAsync("app");

        Assert.Equal("Test host", app.HostName);
        Assert.Equal("es-ES", app.Environment?.Culture);

        await bus.Broker.SetEnvironmentAsync(new AppEnvironment { Culture = "en-US" });

        await BusHarness.Until(() => app.Environment?.Culture == "en-US");
    }

    [Fact]
    public async Task The_host_can_ask_an_application_to_close_and_hears_its_answer()
    {
        await using BusHarness bus = new();
        AppBusClient willing = await bus.ConnectAsync("willing");
        AppBusClient busy = await bus.ConnectAsync("busy");

        string? reason = null;
        willing.CloseHandler = (why, _) => { reason = why; return Task.FromResult(true); };
        busy.CloseHandler = (_, _) => Task.FromResult(false);

        Assert.True(await bus.Broker.RequestCloseAsync("willing", CloseReasons.Update, TimeSpan.FromSeconds(10)));
        Assert.Equal(CloseReasons.Update, reason);
        Assert.False(await bus.Broker.RequestCloseAsync("busy", CloseReasons.User, TimeSpan.FromSeconds(10)));
        Assert.Null(await bus.Broker.RequestCloseAsync("absent", CloseReasons.User, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task An_application_that_closes_the_moment_it_agrees_is_still_heard_agreeing()
    {
        await using BusHarness bus = new();
        AppBusClient quick = await bus.ConnectAsync("quick");

        string? closedFor = null;
        quick.CloseHandler = (_, _) => Task.FromResult(true);

        // What a desktop application does: shut down, taking the connection with it, right away.
        quick.CloseAgreed = reason =>
        {
            closedFor = reason;
            _ = quick.DisposeAsync();
        };

        Assert.True(await bus.Broker.RequestCloseAsync("quick", CloseReasons.Update, TimeSpan.FromSeconds(10)));

        await BusHarness.Until(() => !bus.Broker.IsConnected("quick"));
        Assert.Equal(CloseReasons.Update, closedFor);
    }

    [Fact]
    public async Task An_application_that_refuses_to_close_is_not_closed()
    {
        await using BusHarness bus = new();
        AppBusClient busy = await bus.ConnectAsync("busy");

        bool closed = false;
        busy.CloseHandler = (_, _) => Task.FromResult(false);
        busy.CloseAgreed = _ => closed = true;

        Assert.False(await bus.Broker.RequestCloseAsync("busy", CloseReasons.User, TimeSpan.FromSeconds(10)));
        Assert.False(closed);
    }

    [Fact]
    public async Task The_host_can_bring_an_application_to_the_front()
    {
        await using BusHarness bus = new();
        AppBusClient app = await bus.ConnectAsync("app");

        TaskCompletionSource activated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        app.ActivateRequested += (_, _) => activated.TrySetResult();

        Assert.True(await bus.Broker.ActivateAsync("app"));
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(10), Token);
    }

    [Fact]
    public async Task The_broker_lists_who_is_connected_and_what_each_answers()
    {
        await using BusHarness bus = new();
        await bus.ConnectAsync("echoer", services => services.AddAppRequestHandler<Echo, EchoReply, EchoHandler>());

        ConnectedApp app = Assert.Single(bus.Broker.Apps);

        Assert.Equal("echoer", app.AppId);
        Assert.Equal(["tests.echo"], app.Hello.Handles);
        Assert.Equal("abcdef1234567", app.Hello.Commit);
        Assert.Equal(Environment.ProcessId, app.Hello.ProcessId);
    }
}
