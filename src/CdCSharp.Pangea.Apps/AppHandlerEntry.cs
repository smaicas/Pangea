using CdCSharp.Pangea.Apps.Wire;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace CdCSharp.Pangea.Apps;

/// <summary>
/// One request handler of this application: the message id it answers and how to run it.
/// </summary>
/// <remarks>
/// Built by generated code, one per <see cref="IAppRequestHandler{TRequest,TResponse}"/> the
/// application declares, so that answering a request is a plain generic call rather than a type
/// looked up by name. <see cref="AppsServiceCollectionExtensions.AddAppRequestHandler{TRequest,TResponse,THandler}"/>
/// builds the same thing by hand, for a project the generator did not run in.
/// </remarks>
public sealed class AppHandlerEntry
{
    private readonly Func<IServiceProvider, JsonElement?, AppRequestContext, CancellationToken, Task<JsonElement>> _invoke;

    private AppHandlerEntry(
        string messageId,
        Type requestType,
        Type responseType,
        Type handlerType,
        Func<IServiceProvider, JsonElement?, AppRequestContext, CancellationToken, Task<JsonElement>> invoke)
    {
        MessageId = messageId;
        RequestType = requestType;
        ResponseType = responseType;
        HandlerType = handlerType;
        _invoke = invoke;
    }

    public string MessageId { get; }

    public Type RequestType { get; }

    public Type ResponseType { get; }

    public Type HandlerType { get; }

    /// <summary>Reads the request, runs the handler built from <paramref name="services"/>, and serialises the answer.</summary>
    public Task<JsonElement> InvokeAsync(
        IServiceProvider services, JsonElement? payload, AppRequestContext context, CancellationToken cancellationToken) =>
        _invoke(services, payload, context, cancellationToken);

    /// <summary>The entry for <typeparamref name="THandler"/>. Called from generated code.</summary>
    /// <param name="messageId">The request's <see cref="AppMessageAttribute.Id"/>, read at compile time.</param>
    public static AppHandlerEntry Create<TRequest, TResponse, THandler>(string messageId)
        where TRequest : IAppRequest<TResponse>
        where THandler : IAppRequestHandler<TRequest, TResponse>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        return new AppHandlerEntry(messageId, typeof(TRequest), typeof(TResponse), typeof(THandler), InvokeAsync);

        static async Task<JsonElement> InvokeAsync(
            IServiceProvider services, JsonElement? payload, AppRequestContext context, CancellationToken cancellationToken)
        {
            TRequest request = (payload is { } json ? json.Deserialize<TRequest>(WireJson.Options) : default)
                ?? throw new InvalidDataException($"The request '{typeof(TRequest).Name}' arrived without its data.");

            // A scope per request, so a handler that asks for scoped services - a database context -
            // gets its own and does not share one with a request running beside it.
            using IServiceScope scope = services.CreateScope();

            THandler handler = ActivatorUtilities.GetServiceOrCreateInstance<THandler>(scope.ServiceProvider);
            TResponse response = await handler.HandleAsync(request, context, cancellationToken).ConfigureAwait(false);

            return JsonSerializer.SerializeToElement(response, WireJson.Options);
        }
    }
}

/// <summary>Where the generated handler entries collect, one list per process.</summary>
/// <remarks>
/// Static for the reason the startup catalogs are: an entry describes a type in an assembly, and a
/// module initializer registers it once, when the assembly loads, before any container exists.
/// </remarks>
public static class AppHandlerCatalog
{
    private static readonly object Gate = new();
    private static readonly List<AppHandlerEntry> Entries = [];

    public static IReadOnlyList<AppHandlerEntry> All
    {
        get
        {
            lock (Gate) return Entries.ToArray();
        }
    }

    /// <summary>Registers <paramref name="entry"/>. Called from generated code; a second entry for the same handler is ignored.</summary>
    public static void Add(AppHandlerEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (Gate)
        {
            if (Entries.Any(existing => existing.HandlerType == entry.HandlerType && existing.MessageId == entry.MessageId)) return;

            Entries.Add(entry);
        }
    }
}
