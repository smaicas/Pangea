# PangeaOmniApp

An Avalonia + Pangea application made to live in a host such as Omni, and to run on its own just the same.

| Project | What it is |
|---|---|
| `PangeaOmniApp` | The application: its manifest (`PangeaAppId` and friends in the `.csproj`), a window, and a request handler |
| `PangeaOmniApp.Protocol` | What other applications can ask it. Only `CdCSharp.Pangea.Apps.Abstractions`: pack it for the others to reference |
| `PangeaOmniApp.Tests` | The handler, called directly, and the screen with the bus and the host replaced by the doubles of `CdCSharp.Pangea.Apps.Testing` |

## In an Omni workspace

1. Generate it in `apps/`: `dotnet new pangea-omni -n Notes -o apps/notes`.
2. Add it to `workspace.json` and run `eng/sync-slnx.ps1`.
3. When another application should talk to it, pack the protocol into the feed:
   `dotnet pack PangeaOmniApp.Protocol -c Release -o ../../feed` (or `eng/pack-protocols.ps1`).

Omni then lists it, builds and installs it when its code changes, starts it for a request nobody
running answers, and gives it its theme while connected. The application connects by itself: started
by Omni, from an IDE, or by hand.

## Talking to other applications

```csharp
Greeting greeting = await bus.SendAsync(new MakeGreeting("Ada"));
```

`IAppBus` comes from the container. Without a host it throws `AppBusUnavailableException`; with one,
the host routes the request to whichever application declares a handler for its id - starting it if it
has to. Answering is a class implementing `IAppRequestHandler<TRequest, TResponse>`, found by the
generator. The Pangea skill has the rest, in `references/apps.md`: in a workspace it lives once, at the
workspace root (`.claude/skills/pangea`), so the project carries no copy; generate with
`--IncludeSkill true` for an application worked on outside a workspace.

```bash
dotnet build
dotnet test
dotnet run --project PangeaOmniApp
```
