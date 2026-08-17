# Extending Pangea

How to add a capability to the toolkit itself. Writing an application does not require any of this;
this is for work on Pangea, or for an application that ships a Pangea feature of its own.

## The feature contract

```csharp
using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

public class TelemetryFeature : IPangeaFeature
{
    public string Name => "Telemetry";
    public Version Version => new(1, 0, 0);

    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<ITelemetry, Telemetry>();

    public void ConfigureApplication(IServiceProvider services, IPangeaApplicationContext context)
    {
        // Runs after the container is built, with the application available.
    }
}
```

Discovery is by interface: any non-abstract `IPangeaFeature` in a scanned assembly is instantiated
and registered. It needs a public parameterless constructor. A feature that throws while configuring
**aborts startup** naming itself — that is intentional, a half-configured feature is worse than none.

Assemblies reachable from the entry assembly are scanned automatically. For anything else:

```csharp
using CdCSharp.Pangea;
using CdCSharp.Pangea.Core.Configuration;

public partial class PluginApp : PangeaApplication
{
    public override PangeaOptions ConfigurePangeaOptions(PangeaOptions options)
    {
        options.DI.AdditionalAssemblies.Add(typeof(TelemetryFeature).Assembly);
        return options;   // the return value is what gets used
    }
}
```

---
