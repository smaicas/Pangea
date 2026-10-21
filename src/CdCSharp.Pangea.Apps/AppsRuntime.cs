using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CdCSharp.Pangea.Apps.Wire;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace CdCSharp.Pangea.Apps;

/// <summary>
/// Puts the pieces together once the application is up: the single-instance lock, the bus, and
/// what the host asks of the application - to come to the front, to close, to wear its theme.
/// </summary>
internal sealed class AppsRuntime(
    AppBusClient client,
    IServiceProvider services,
    IOptions<AppsOptions> options,
    ILogger<AppsRuntime> logger)
{
    private SingleInstanceGuard? _guard;

    public void Start()
    {
        AppsOptions settings = options.Value;

        // A process with no desktop lifetime - a test host, a designer - is not a running copy of
        // the application, and must not take its lock or connect as it.
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

        if (settings.SingleInstance && !TakeSingleInstance(settings, desktop)) return;

        client.ActivateRequested += (_, _) => BringToFront();
        client.CloseHandler = DecideCloseAsync;
        client.CloseAgreed = _ => Shutdown();

        ActivatorUtilities.CreateInstance<HostEnvironmentApplier>(services).Attach();

        if (settings.ConnectToHost) client.Start();
    }

    /// <summary>
    /// Takes the lock, or hands over to the copy that has it.
    /// </summary>
    /// <returns><see langword="false"/> when this process is on its way out.</returns>
    private bool TakeSingleInstance(AppsOptions settings, IClassicDesktopStyleApplicationLifetime desktop)
    {
        _guard = new SingleInstanceGuard(client.Identity.Id, logger);

        if (!_guard.TryAcquire(TimeSpan.Zero))
        {
            // Blocking is fine here: startup has not shown anything yet, and SendAsync never comes
            // back to this thread.
            if (settings.ReplaceRunningInstance || Debugger.IsAttached)
            {
                string? answer = _guard.SendAsync(SingleInstanceGuard.ReplaceCommand, TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();

                if (answer != "ok" || !_guard.TryAcquire(TimeSpan.FromSeconds(15)))
                {
                    logger.LogWarning("{App} is already running and did not hand over ({Answer}); this copy exits", client.Identity.Name, answer ?? "no answer");
                    Environment.Exit(0);
                    return false;
                }

                logger.LogInformation("Took over from the running copy of {App}", client.Identity.Name);
            }
            else
            {
                _guard.SendAsync(SingleInstanceGuard.ActivateCommand, TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
                Environment.Exit(0);
                return false;
            }
        }

        _guard.Listen(
            async command => command switch
            {
                SingleInstanceGuard.ActivateCommand => Activate(),
                SingleInstanceGuard.ReplaceCommand => await DecideCloseAsync(CloseReasons.Replace, CancellationToken.None).ConfigureAwait(false) ? "ok" : "refused",
                _ => "unknown"
            },
            (command, answer) =>
            {
                if (command == SingleInstanceGuard.ReplaceCommand && answer == "ok") Shutdown();
            });

        desktop.Exit += (_, _) => _guard.Dispose();
        return true;

        string Activate()
        {
            BringToFront();
            return "ok";
        }
    }

    private void BringToFront() => services.GetRequiredService<IUIDispatcher>().Post(() =>
    {
        if (services.GetService<IWindowManager>()?.GetMainWindow() is not { } window) return;

        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;

        window.Show();
        window.Activate();

        // Activate alone is ignored by Windows when the process is not in the foreground; a
        // moment on top gets the window in front of whatever is.
        window.Topmost = true;
        window.Topmost = false;
    });

    /// <summary>Asks the application's close handlers whether it may close. Decides only; <see cref="Shutdown"/> closes.</summary>
    private async Task<bool> DecideCloseAsync(string reason, CancellationToken cancellationToken)
    {
        foreach (IAppCloseHandler handler in services.GetServices<IAppCloseHandler>())
        {
            if (!await handler.OnCloseRequestedAsync(reason, cancellationToken).ConfigureAwait(false))
            {
                logger.LogInformation("Asked to close ({Reason}); {Handler} kept the application open", reason, handler.GetType().Name);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Closes, after the answer has gone out. The windows still have their say through
    /// <c>Closing</c>; one that cancels leaves the application open, and the host, which waits for
    /// the process to end, notices.
    /// </summary>
    private void Shutdown() => services.GetRequiredService<IUIDispatcher>().Post(() =>
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && !desktop.TryShutdown())
        {
            logger.LogInformation("Agreed to close, but a window kept the application open");
        }
    });
}
