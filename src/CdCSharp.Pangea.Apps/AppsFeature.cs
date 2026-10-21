using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Apps;

/// <summary>
/// Makes the application part of a host: the bus, the single-instance lock, and the host's
/// environment. Registered by being installed, like every Pangea feature.
/// </summary>
/// <remarks>
/// Nothing here is required to run. Without a host the application is a standalone application
/// with a disconnected bus; with one, it shows up in it, answers what it handles and wears what
/// the host decided.
/// </remarks>
public sealed class AppsFeature : IPangeaFeature
{
    public string Name => "Apps";

    public Version Version => new(1, 0, 0);

    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<AppsOptions>(_ => { });

        services.TryAddSingleton(provider => AppIdentity.Resolve(provider.GetRequiredService<IOptions<AppsOptions>>().Value));
        services.TryAddSingleton<AppBusClient>();
        services.TryAddSingleton<IAppBus>(provider => provider.GetRequiredService<AppBusClient>());
        services.TryAddSingleton<IAppHostContext>(provider => provider.GetRequiredService<AppBusClient>());
        services.TryAddSingleton<AppsRuntime>();
    }

    public void ConfigureApplication(IServiceProvider services, IPangeaApplicationContext context) =>
        services.GetRequiredService<AppsRuntime>().Start();
}

public static class AppsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a request handler by hand. The generator does this for every handler an
    /// application declares, so this is for a project it did not run in - a test, typically.
    /// </summary>
    public static IServiceCollection AddAppRequestHandler<TRequest, TResponse, THandler>(this IServiceCollection services)
        where TRequest : IAppRequest<TResponse>
        where THandler : IAppRequestHandler<TRequest, TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(AppHandlerEntry.Create<TRequest, TResponse, THandler>(AppMessages.IdOf<TRequest>()));
        return services;
    }
}
