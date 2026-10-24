# PangeaOmniApp

An Avalonia + Pangea application made to live in a host such as Omni, and to run on its own just the same.

| Project | What it is |
|---|---|
| `src/PangeaOmniApp` | The application: its manifest (`PangeaAppId` and friends in the `.csproj`), a window, a palette, and a request handler |
| `src/PangeaOmniApp.Protocol` | What other applications can ask it. Only `CdCSharp.Pangea.Apps.Abstractions`: pack it for the others to reference |
| `test/PangeaOmniApp.Tests` | The handler, called directly, and the screen with the bus and the host replaced by the doubles of `CdCSharp.Pangea.Apps.Testing` |

What every project shares is in `Directory.Build.props`, and every package version in
`Directory.Packages.props`: the project files only name packages. Warnings are errors.

## In an Omni workspace

1. Generate it in `apps/`: `dotnet new pangea-omni -n Notes -o apps/notes`.
2. Start its repository: `git init` in `apps/notes`.
3. Add it to `workspace.json` and run `eng/sync-slnx.ps1`.
4. When another application should talk to it, pack the protocol into the feed:
   `dotnet pack src/PangeaOmniApp.Protocol -c Release -o ../../feed` (or `eng/pack-protocols.ps1`).

Omni then lists it, builds and installs it when its code changes, starts it for a request nobody
running answers, and gives it its theme while connected. The application connects by itself: started
by Omni, from an IDE, or by hand.

## Its look

`Themes/PangeaOmniAppPalette.cs` holds the application's colours while it runs alone; inside a host,
the host's palette replaces them. `App.axaml` brings `PangeaStyles` - the classes (`title`, `hint`,
`card`, `accent`, `nav`...) and `Icon.*` geometries the applications of a host share - so the
application looks like one of the family before it has a style of its own.

## Talking to other applications

```csharp
Greeting greeting = await bus.SendAsync(new MakeGreeting("Ada"));
```

`IAppBus` comes from the container. Without a host it throws `AppBusUnavailableException`; with one,
the host routes the request to whichever application declares a handler for its id - starting it if it
has to. Answering is a class implementing `IAppRequestHandler<TRequest, TResponse>`, found by the
generator. To ask another application, add its `*.Protocol` package to `Directory.Packages.props`
and reference it. The Pangea skill has the rest, in `references/apps.md`: in a workspace it lives
once, at the workspace root (`.claude/skills/pangea`), so the project carries no copy; generate with
`--IncludeSkill true` for an application worked on outside a workspace.

```bash
dotnet build
dotnet test
dotnet run --project src/PangeaOmniApp
```
