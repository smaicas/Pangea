using CdCSharp.Pangea.Apps;
using PangeaOmniApp.Handlers;
using PangeaOmniApp.Protocol;
using PangeaOmniApp.Services;

namespace PangeaOmniApp.Tests;

/// <summary>
/// A handler is a plain class with a plain method, so it is tested by calling it: no bus, no host.
/// What another application gets back is exactly what this returns.
/// </summary>
public class MakeGreetingHandlerTests
{
    [Fact]
    public async Task AGreetingSaysTheNameAndRecordsWhoAsked()
    {
        GreetingLog log = new();
        MakeGreetingHandler handler = new(log);

        Greeting greeting = await handler.HandleAsync(
            new MakeGreeting("Ada") { Language = "es" }, new AppRequestContext("other-app"), CancellationToken.None);

        Assert.Equal("¡Hola, Ada!", greeting.Text);
        Assert.Equal("other-app", log.LastCaller);
        Assert.Equal("Ada", log.LastName);
    }

    [Fact]
    public async Task ARequestWithoutANameFailsWithAMessageForTheCaller()
    {
        MakeGreetingHandler handler = new(new GreetingLog());

        ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(new MakeGreeting(" "), new AppRequestContext("other-app"), CancellationToken.None));

        Assert.Equal("Say who to greet.", ex.Message);
    }

    [Fact]
    public void TheMessageCarriesTheIdItTravelsUnder()
    {
        Assert.StartsWith("pangeaomniapp.", AppMessages.IdOf<MakeGreeting>());
    }
}
