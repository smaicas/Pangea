using CdCSharp.Pangea.Apps;
using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using Microsoft.Extensions.DependencyInjection;
using PangeaOmniApp.Protocol;
using PangeaOmniApp.Services;

namespace PangeaOmniApp.ViewModels;

/// <summary>
/// Whether the application is inside a host right now, and a round trip over the bus to prove it.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAppHostContext _host;
    private readonly IAppBus _bus;
    private readonly GreetingLog _log;
    private readonly IUIDispatcher _ui;

    public MainWindowViewModel(IServiceProvider services) : base(services)
    {
        _host = services.GetRequiredService<IAppHostContext>();
        _bus = services.GetRequiredService<IAppBus>();
        _log = services.GetRequiredService<GreetingLog>();
        _ui = services.GetRequiredService<IUIDispatcher>();

        // Both are raised on background threads; the handlers move to the UI thread.
        Subscribe(handler => _host.Changed += handler, handler => _host.Changed -= handler, OnHostChanged);
        Subscribe(handler => _log.Recorded += handler, handler => _log.Recorded -= handler, OnGreetingRecorded);

        UpdateHost();
    }

    [Binding] private bool _isHosted;
    [Binding] private string _hostText = "";
    [Binding] private string _name = "Ada";
    [Binding] private string? _answer;
    [Binding] private string? _lastRequest;

    public string Title => _host.Identity.Name;

    public bool CanGreet => !string.IsNullOrWhiteSpace(Name);

    /// <summary>
    /// Sends <see cref="MakeGreeting"/> over the bus. This application answers it itself, through
    /// the host - which is the whole path another application's request takes.
    /// </summary>
    public RelayCommand GreetCommand => CreateCommand(GreetAsync, () => CanGreet);

    private async Task GreetAsync()
    {
        try
        {
            Greeting greeting = await _bus.SendAsync(new MakeGreeting(Name));
            Answer = greeting.Text;
        }
        catch (AppBusUnavailableException)
        {
            Answer = "No host is running: open Omni and try again.";
        }
        catch (AppHandlerNotFoundException)
        {
            Answer = "Nobody answers that message.";
        }
        catch (AppRequestFailedException ex)
        {
            Answer = ex.Message;
        }
    }

    private void OnHostChanged(object? sender, EventArgs e) => _ui.Post(UpdateHost);

    private void OnGreetingRecorded(object? sender, EventArgs e) =>
        _ui.Post(() => LastRequest = $"{_log.LastCaller} asked to greet {_log.LastName}.");

    private void UpdateHost()
    {
        IsHosted = _host.IsHosted;
        HostText = _host.IsHosted ? $"Connected to {_host.HostName}" : "Running on its own";
    }
}
