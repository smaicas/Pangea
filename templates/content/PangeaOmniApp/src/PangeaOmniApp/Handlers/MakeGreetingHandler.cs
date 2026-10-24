using CdCSharp.Pangea.Apps;
using PangeaOmniApp.Protocol;
using PangeaOmniApp.Services;

namespace PangeaOmniApp.Handlers;

/// <summary>Answers <see cref="MakeGreeting"/> for any application that asks.</summary>
/// <remarks>
/// Declaring it is all there is: the generator registers it and lists its message id in the
/// manifest, which is how the host knows to start this application for that request. It is built
/// from the container for each request, so it asks for services in its constructor.
/// </remarks>
public sealed class MakeGreetingHandler(GreetingLog log) : IAppRequestHandler<MakeGreeting, Greeting>
{
    public Task<Greeting> HandleAsync(MakeGreeting request, AppRequestContext context, CancellationToken cancellationToken)
    {
        // An exception thrown here reaches the caller with its message: write it for the person
        // who will read it in the other application.
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Say who to greet.");

        log.Record(context.CallerId, request.Name);

        string text = request.Language == "es" ? $"¡Hola, {request.Name}!" : $"Hello, {request.Name}!";
        return Task.FromResult(new Greeting(text));
    }
}
