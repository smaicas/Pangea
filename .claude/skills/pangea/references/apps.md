# Applications in a host: CdCSharp.Pangea.Apps

An application that lives beside others - started, updated and connected by a host such as Omni -
and still runs perfectly well on its own.

| Package | For |
|---|---|
| `CdCSharp.Pangea.Apps` | The application: manifest, bus client, one instance, the host's theme |
| `CdCSharp.Pangea.Apps.Abstractions` | A protocol library: the messages, and nothing else - no Avalonia |
| `CdCSharp.Pangea.Apps.Hosting` | The host: `AppBroker` and `AppManifestReader` |

Installing `CdCSharp.Pangea.Apps` is the whole setup. `AppsFeature` registers itself like every
feature; without a host running, nothing changes for the application.

## The manifest

Four MSBuild properties in the application's project, turned into assembly attributes a host reads
**without running the application**:

```xml
<PropertyGroup>
  <PangeaAppId>yutuf</PangeaAppId>                         <!-- stable, lower case -->
  <PangeaAppName>Yutuf</PangeaAppName>
  <PangeaAppDescription>Downloads music.</PangeaAppDescription>
  <PangeaAppIcon>Assets\icon.png</PangeaAppIcon>           <!-- copied beside the executable -->
</PropertyGroup>
```

The id names the single-instance lock and is how the host finds the application. Never change it.

## A protocol: what other applications can ask

Put the messages in a library of their own, `<App>.Protocol`, that references only
`CdCSharp.Pangea.Apps.Abstractions`, and publish it as a package for the others to reference.

```csharp
using CdCSharp.Pangea.Apps;

namespace AppsSamples.Protocol;

// The id travels, not the type: app.area.verb, the owning application first.
[AppMessage("sample.song.download")]
public sealed record DownloadSong(string Query) : IAppRequest<DownloadSongResult>
{
    // Optional members can be added later without breaking anyone.
    public string? RequestedBy { get; init; }
}

public sealed record DownloadSongResult(string FilePath, string Title);

[AppMessage("sample.song.progress")]
public sealed record SongProgress(string Title, double Fraction) : IAppEvent;
```

- Records with `init` members: the wire is JSON, read by name, so an added optional member is
  invisible to an older reader and a removed one is just missing.
- Enums travel as their names. Renaming a member or an `[AppMessage]` id is a breaking change.
- Files stay on disk; send their paths. A frame is limited to 16 MB.

## Answering: a handler

```csharp
using AppsSamples.Protocol;
using System.IO;
using System.Threading;
using CdCSharp.Pangea.Apps;

namespace AppsSamples.Handlers;

public interface ISongStore
{
    Task<string> DownloadAsync(string query, CancellationToken cancellationToken);
}

// Declaring it is all: the generator registers it and lists the id in the manifest, so the host
// knows to start this application for that request. Built from the container for each request.
internal sealed class DownloadSongHandler(ISongStore songs) : IAppRequestHandler<DownloadSong, DownloadSongResult>
{
    public async Task<DownloadSongResult> HandleAsync(DownloadSong request, AppRequestContext context, CancellationToken cancellationToken)
    {
        // context.CallerId is the id of the application that asked.
        string path = await songs.DownloadAsync(request.Query, cancellationToken);
        return new DownloadSongResult(path, Path.GetFileNameWithoutExtension(path));
    }
}
```

An exception thrown here reaches the caller as `AppRequestFailedException` with its message: write
the message for the person who will read it there. The generator refuses a handler whose request
has no `[AppMessage]` (`PGA001`), two handlers for one id (`PGA002`), and a private handler (`PGA003`).

## Asking: the bus

```csharp
using AppsSamples.Protocol;
using System.IO;
using System.Threading;
using CdCSharp.Pangea.Apps;
using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;
using Microsoft.Extensions.DependencyInjection;

namespace AppsSamples.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
    private readonly IAppBus _bus;

    public LibraryViewModel(IServiceProvider services) : base(services) =>
        _bus = services.GetRequiredService<IAppBus>();

    [Binding] private string _query = "";
    [Binding] private string? _status;

    public RelayCommand AddCommand => CreateCommand(AddAsync);

    private async Task AddAsync()
    {
        try
        {
            DownloadSongResult song = await _bus.SendAsync(new DownloadSong(Query) { RequestedBy = "Library" });
            Status = $"{song.Title} added.";
        }
        catch (AppBusUnavailableException)
        {
            Status = "Open the host to do this.";
        }
        catch (AppHandlerNotFoundException)
        {
            Status = "No installed application can do this.";
        }
        catch (AppRequestFailedException ex)
        {
            Status = ex.Message;
        }
    }
}
```

- **Nobody has to be running.** A request nobody connected answers makes the host start the
  installed application whose manifest declares it, and wait for it to connect.
- `CanSendAsync<TRequest>()` says whether anyone could answer - connected, or installed - which is
  what decides whether to show the button at all. It is `false` without a host.
- Cancelling the token cancels the handler in the other application.
- `PublishAsync` and `Subscribe<TEvent>` are events: fire and forget, to every subscriber but the
  publisher. A subscriber runs on a background thread; dispose the subscription with the screen.
- The bus reconnects on its own when the host starts later, and keeps subscriptions across it.

## Closing when the host asks

The host asks before an update and when the person stops the application from it. Say no while
something must not be interrupted; tidy up when saying yes.

```csharp
using CdCSharp.Pangea.Apps;
using CdCSharp.Pangea.Apps.Wire;
using System.Threading;

namespace AppsSamples.Lifecycle;

public interface IJobRunner
{
    bool IsRunning { get; }
}

public sealed class RunningJobCloseHandler(IJobRunner jobs) : IAppCloseHandler
{
    public Task<bool> OnCloseRequestedAsync(string reason, CancellationToken cancellationToken) =>
        // CloseReasons.Update, User or Replace: refuse any of them while the job runs.
        Task.FromResult(!jobs.IsRunning);
}
```

Register it with `services.AddSingleton<IAppCloseHandler, RunningJobCloseHandler>()`. The answer
goes back to the host first, and only then does the application shut down.

## What the host decides

While connected, the application wears the host's palette and variant (its own metrics stay) and its
culture when the application supports it; disconnected, its own come back. A screen that offers a
theme choice should disable it while `IAppHostContext.IsHosted` - and must not apply its own variant
then, or it undoes the host's.

`AppsOptions` turns pieces off: `FollowHostTheme`, `FollowHostCulture`, `SingleInstance`,
`ConnectToHost`.

## One copy at a time

A second start brings the running copy to the front and exits. Started **under a debugger**, it asks
the running copy to close and takes over - so F5 replaces the installed copy while working on it.

## The host's side

```csharp
using CdCSharp.Pangea.Apps;
using CdCSharp.Pangea.Apps.Hosting;

namespace AppsSamples.Host;

public static class MiniHost
{
    public static async Task RunAsync(Func<string, AppManifest?> installedFor, Action<AppManifest> start)
    {
        await using AppBroker broker = new(new AppBrokerOptions
        {
            HostName = "Mini host",
            Environment = new AppEnvironment { Culture = "es-ES" },
            CanLaunch = message => installedFor(message) is not null,
            LaunchHandler = (message, _) =>
            {
                if (installedFor(message) is not { } manifest) return Task.FromResult(false);

                start(manifest);   // with PANGEA_APPS_PIPE and PANGEA_APPS_HOST in its environment
                return Task.FromResult(true);
            }
        });

        broker.Start();

        bool? agreed = await broker.RequestCloseAsync("sample", "update", TimeSpan.FromSeconds(30));
        Console.WriteLine(agreed);
    }
}
```

`AppManifestReader.TryRead(path)` reads a manifest - id, name, version, commit, icon, handled ids -
from an assembly's metadata, without loading it.
