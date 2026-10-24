using CdCSharp.Pangea.Apps.Testing;
using CdCSharp.Pangea.Testing;
using PangeaOmniApp.Protocol;
using PangeaOmniApp.Services;
using PangeaOmniApp.ViewModels;

namespace PangeaOmniApp.Tests;

/// <summary>
/// The screen with the bus and the host replaced by doubles: what it says when there is a host,
/// when there is none, and what it does with an answer.
/// </summary>
public class MainWindowViewModelTests
{
    [Fact]
    public void OnItsOwnTheScreenSaysSo()
    {
        MainWindowViewModel screen = new(Services());

        Assert.False(screen.IsHosted);
        Assert.Equal("Running on its own", screen.HostText);
    }

    [Fact]
    public void ConnectingToAHostIsShownAsItHappens()
    {
        PangeaTestServices services = Services();
        MainWindowViewModel screen = new(services);

        services.AppHost().Connect("Omni");

        Assert.True(screen.IsHosted);
        Assert.Equal("Connected to Omni", screen.HostText);
    }

    [Fact]
    public async Task TheAnswerOfTheBusIsShown()
    {
        PangeaTestServices services = Services();
        services.AppBus().Answer((MakeGreeting request) => new Greeting($"Hello, {request.Name}!"));
        MainWindowViewModel screen = new(services);

        await screen.GreetCommand.ExecuteAsync(null);

        Assert.Equal("Hello, Ada!", screen.Answer);
        Assert.Equal("Ada", services.AppBus().LastSent<MakeGreeting>()?.Name);
    }

    [Fact]
    public async Task WithoutAHostTheScreenSaysWhatToDo()
    {
        PangeaTestServices services = Services();
        services.AppBus().IsConnected = false;
        MainWindowViewModel screen = new(services);

        await screen.GreetCommand.ExecuteAsync(null);

        Assert.Equal("No host is running: open Omni and try again.", screen.Answer);
    }

    [Fact]
    public async Task AFailingAnswerIsShownAsTheOtherApplicationWroteIt()
    {
        PangeaTestServices services = Services();
        services.AppBus().Fail<MakeGreeting>("Say who to greet.");
        MainWindowViewModel screen = new(services);

        await screen.GreetCommand.ExecuteAsync(null);

        Assert.Equal("Say who to greet.", screen.Answer);
    }

    // AddApps registers a bus that answers from a script and a host that connects on cue, with
    // AppBus() and AppHost() to reach them.
    private static PangeaTestServices Services() => new PangeaTestServices()
        .AddApps()
        .Add(new GreetingLog());
}
