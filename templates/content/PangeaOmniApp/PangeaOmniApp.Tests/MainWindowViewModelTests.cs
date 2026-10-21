using CdCSharp.Pangea.Apps;
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
        MainWindowViewModel screen = new(Services(new FakeHost(), new FakeBus()));

        Assert.False(screen.IsHosted);
        Assert.Equal("Running on its own", screen.HostText);
    }

    [Fact]
    public void ConnectingToAHostIsShownAsItHappens()
    {
        FakeHost host = new();
        MainWindowViewModel screen = new(Services(host, new FakeBus()));

        host.Connect("Omni");

        Assert.True(screen.IsHosted);
        Assert.Equal("Connected to Omni", screen.HostText);
    }

    [Fact]
    public async Task TheAnswerOfTheBusIsShown()
    {
        FakeBus bus = new() { Answer = new Greeting("Hello, Ada!") };
        MainWindowViewModel screen = new(Services(new FakeHost(), bus));

        await screen.GreetCommand.ExecuteAsync(null);

        Assert.Equal("Hello, Ada!", screen.Answer);
        Assert.Equal("Ada", Assert.IsType<MakeGreeting>(Assert.Single(bus.Sent)).Name);
    }

    [Fact]
    public async Task WithoutAHostTheScreenSaysWhatToDo()
    {
        MainWindowViewModel screen = new(Services(new FakeHost(), new FakeBus { Unavailable = true }));

        await screen.GreetCommand.ExecuteAsync(null);

        Assert.Equal("No host is running: open Omni and try again.", screen.Answer);
    }

    private static PangeaTestServices Services(FakeHost host, FakeBus bus) => new PangeaTestServices()
        .Add<IAppHostContext>(host)
        .Add<IAppBus>(bus)
        .Add(new GreetingLog());

    private sealed class FakeHost : IAppHostContext
    {
        public AppIdentity Identity { get; } = new("pangeaomniapp", "PangeaOmniApp");

        public bool LaunchedByHost => false;

        public bool IsHosted { get; private set; }

        public string? HostName { get; private set; }

        public AppEnvironment? Environment => null;

        public event EventHandler? Changed;

        public void Connect(string host)
        {
            IsHosted = true;
            HostName = host;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeBus : IAppBus
    {
        public object? Answer { get; init; }

        public bool Unavailable { get; init; }

        public List<object> Sent { get; } = [];

        public bool IsConnected => !Unavailable;

        public event EventHandler? ConnectionChanged { add { } remove { } }

        public Task<TResponse> SendAsync<TResponse>(IAppRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (Unavailable) throw new AppBusUnavailableException("No host.");

            Sent.Add(request);
            return Task.FromResult((TResponse)Answer!);
        }

        public Task<bool> CanSendAsync<TRequest>(CancellationToken cancellationToken = default) where TRequest : IAppMessage =>
            Task.FromResult(!Unavailable);

        public Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default) where TEvent : IAppEvent =>
            Task.CompletedTask;

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IAppEvent => new Nothing();

        private sealed class Nothing : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
