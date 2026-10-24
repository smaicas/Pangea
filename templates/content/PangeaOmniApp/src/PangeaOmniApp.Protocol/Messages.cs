using CdCSharp.Pangea.Apps;

namespace PangeaOmniApp.Protocol;

/// <summary>Asks this application for a greeting. Replace it with what your application really offers.</summary>
/// <remarks>
/// The id travels instead of the type: the application's id first, then what it is about and what
/// it does. Another application sends it with <c>IAppBus.SendAsync(new MakeGreeting("Ada"))</c>,
/// and the host starts this one first if it is not running.
/// </remarks>
[AppMessage("pangeaomniapp.greeting.make")]
public sealed record MakeGreeting(string Name) : IAppRequest<Greeting>
{
    /// <summary>Optional members can be added later without breaking anyone.</summary>
    public string? Language { get; init; }
}

public sealed record Greeting(string Text);
