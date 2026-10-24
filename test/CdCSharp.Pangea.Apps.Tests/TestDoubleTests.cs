using CdCSharp.Pangea.Apps.Testing;
using CdCSharp.Pangea.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Apps.Tests;

/// <summary>
/// The doubles fail the way the real bus does, so what a screen does for each failure can be tested.
/// </summary>
public class TestDoubleTests
{
    [Fact]
    public async Task A_scripted_answer_is_returned_and_the_request_recorded()
    {
        FakeAppBus bus = new FakeAppBus().Answer((Echo request) => new EchoReply(request.Text + "!", "test"));

        EchoReply reply = await bus.SendAsync(new Echo("hi"), TestContext.Current.CancellationToken);

        Assert.Equal("hi!", reply.Text);
        Assert.Equal("hi", bus.LastSent<Echo>()?.Text);
        Assert.Single(bus.SentOf<Echo>());
        Assert.True(await bus.CanSendAsync<Echo>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Without_a_host_nothing_is_sent()
    {
        FakeAppBus bus = new FakeAppBus { IsConnected = false }.Answer((Echo request) => new EchoReply(request.Text, "test"));

        await Assert.ThrowsAsync<AppBusUnavailableException>(() => bus.SendAsync(new Echo("hi"), TestContext.Current.CancellationToken));

        Assert.Empty(bus.Sent);
        Assert.False(await bus.CanSendAsync<Echo>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_request_nobody_answers_says_so()
    {
        FakeAppBus bus = new();

        AppHandlerNotFoundException missing =
            await Assert.ThrowsAsync<AppHandlerNotFoundException>(() => bus.SendAsync(new Nobody(), TestContext.Current.CancellationToken));

        Assert.Equal("tests.nobody", missing.MessageId);
        Assert.False(await bus.CanSendAsync<Nobody>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_failing_handler_arrives_with_its_message()
    {
        FakeAppBus bus = new FakeAppBus().Fail<Fail>("The file is gone.");

        AppRequestFailedException failed =
            await Assert.ThrowsAsync<AppRequestFailedException>(() => bus.SendAsync(new Fail("x"), TestContext.Current.CancellationToken));

        Assert.Equal("The file is gone.", failed.Message);
        Assert.Equal("tests.fail", failed.MessageId);
    }

    [Fact]
    public async Task Cancelling_reaches_an_answer_that_waits()
    {
        FakeAppBus bus = new FakeAppBus().Answer(async (WaitForever _, CancellationToken cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return true;
        });

        using CancellationTokenSource caller = new(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bus.SendAsync(new WaitForever(), caller.Token));
    }

    [Fact]
    public async Task Raised_events_reach_subscribers_until_they_stop_and_published_ones_are_recorded()
    {
        FakeAppBus bus = new();
        List<string> received = [];

        IDisposable subscription = bus.Subscribe<Happened>(happened => received.Add(happened.What));

        Assert.Equal(1, bus.Raise(new Happened("first")));

        subscription.Dispose();

        Assert.Equal(0, bus.Raise(new Happened("second")));
        Assert.Equal(["first"], received);

        await bus.PublishAsync(new Happened("mine"), TestContext.Current.CancellationToken);

        Assert.Equal("mine", Assert.IsType<Happened>(Assert.Single(bus.Published)).What);
    }

    [Fact]
    public void Connecting_and_disconnecting_are_announced()
    {
        FakeAppBus bus = new();
        FakeAppHost host = new();
        int busChanges = 0, hostChanges = 0;
        bus.ConnectionChanged += (_, _) => busChanges++;
        host.Changed += (_, _) => hostChanges++;

        Assert.False(host.IsHosted);

        host.Connect("Omni");

        Assert.True(host.IsHosted);
        Assert.Equal("Omni", host.HostName);
        Assert.Equal("Omni", host.Environment?.HostName);

        host.Disconnect();
        bus.IsConnected = false;
        bus.IsConnected = false;

        Assert.False(host.IsHosted);
        Assert.Null(host.HostName);
        Assert.Equal(2, hostChanges);
        Assert.Equal(1, busChanges);
    }

    [Fact]
    public void The_test_container_has_what_an_application_in_a_host_asks_for()
    {
        FakeAppBus bus = new();
        PangeaTestServices services = new PangeaTestServices().AddApps(bus);

        Assert.Same(bus, services.GetRequiredService<IAppBus>());
        Assert.Same(bus, services.AppBus());
        Assert.Same(services.AppHost(), services.GetRequiredService<IAppHostContext>());
        Assert.Equal(services.AppHost().Identity, services.GetRequiredService<AppIdentity>());
        Assert.NotNull(services.GetService<AppInteractions>());
        Assert.NotNull(services.GetService<AppActivity>());
        Assert.False(Directory.Exists(services.GetRequiredService<AppRequestFolders>().Root));
    }
}
