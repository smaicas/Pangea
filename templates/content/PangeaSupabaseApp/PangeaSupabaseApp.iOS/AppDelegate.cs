using Avalonia;
using Avalonia.iOS;
using CdCSharp.Pangea;
using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Foundation;

namespace PangeaSupabaseApp.iOS;

/// <summary>
/// The iOS head: an entry point and nothing else.
/// </summary>
/// <remarks>
/// <c>UsePangea</c> is the call that matters: it builds the container, discovers the toolkit's
/// features and registers the view models.
/// </remarks>
[Register(nameof(AppDelegate))]
public partial class AppDelegate : AvaloniaAppDelegate<App>
{
    /// <summary>
    /// Registers what only iOS can build, and then starts the application.
    /// </summary>
    /// <remarks>
    /// UsePangea takes them because this is the only code that is both inside the head and part of
    /// starting the application - the shared library cannot see iOS, and the toolkit's own defaults
    /// are registered with TryAdd so what is registered here wins.
    /// </remarks>
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UsePangea(services =>
            {
                services.AddSingleton<ISecretStore, KeychainSecretStore>();
                services.AddSingleton<IConnectivity, PathMonitorConnectivity>();
            });
}
