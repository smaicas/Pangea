using Avalonia;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Navigation.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Navigation;

public class NavigationFeature : IPangeaFeature
{
    public string Name => "Navigation";
    public Version Version => new(1, 0, 0);

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IViewLocator, ViewLocator>();
        services.AddSingleton<INavigationService, NavigationService>();
    }

    public void ConfigureApplication(IServiceProvider serviceProvider, IPangeaApplicationContext applicationContext)
    {
        PangeaCatalogIndex catalog = serviceProvider.GetService<PangeaCatalogIndex>() ?? PangeaCatalogIndex.Empty;

        if (catalog.IsEmpty)
        {
            VerifyRequestsReachTheirDestination(serviceProvider.GetRequiredService<TypeRegistry>());
        }
        else
        {
            VerifyRequestsReachTheirDestination(catalog);
        }

        // Published on the application because a NavigationHost built by XAML has no constructor
        // to inject into.
        if (Application.Current is { } application)
        {
            NavigationHost.SetApplicationService(application, serviceProvider.GetRequiredService<INavigationService>());
            NavigationHost.SetApplicationLocator(application, serviceProvider.GetRequiredService<IViewLocator>());
        }
    }

    /// <summary>
    /// A request whose destination does not accept it would navigate and silently drop the data.
    /// The whole point of declaring the destination in the type is that this is knowable, so it is
    /// checked once at startup rather than discovered as a screen that loads nothing.
    /// </summary>
    private static void VerifyRequestsReachTheirDestination(TypeRegistry typeRegistry) =>
        VerifyRequestsReachTheirDestination(typeRegistry.GetTypesImplementing(typeof(INavigationRequest<>)));

    /// <summary>
    /// The same check against the generated catalog, which already knows every request and where
    /// each one says it goes.
    /// </summary>
    private static void VerifyRequestsReachTheirDestination(PangeaCatalogIndex catalog)
    {
        List<string> broken = [];

        foreach (PangeaNavigationEntry entry in catalog.NavigationRequests)
        {
            Type expected = typeof(INavigationAware<>).MakeGenericType(entry.RequestType);

            if (!expected.IsAssignableFrom(entry.DestinationType))
            {
                broken.Add($"'{entry.RequestType.Name}' navigates to '{entry.DestinationType.Name}', which does not implement " +
                           $"INavigationAware<{entry.RequestType.Name}> and would never receive it");
            }
        }

        Report(broken);
    }

    internal static void VerifyRequestsReachTheirDestination(IEnumerable<Type> requestTypes)
    {
        List<string> broken = [];

        foreach (Type requestType in requestTypes)
        {
            if (requestType.IsAbstract || requestType.IsInterface) continue;

            foreach (Type declaration in requestType.GetInterfaces())
            {
                if (!declaration.IsGenericType ||
                    declaration.GetGenericTypeDefinition() != typeof(INavigationRequest<>))
                {
                    continue;
                }

                Type destination = declaration.GetGenericArguments()[0];
                Type expected = typeof(INavigationAware<>).MakeGenericType(requestType);

                if (!expected.IsAssignableFrom(destination))
                {
                    broken.Add($"'{requestType.Name}' navigates to '{destination.Name}', which does not implement " +
                               $"INavigationAware<{requestType.Name}> and would never receive it");
                }
            }
        }

        Report(broken);
    }

    private static void Report(List<string> broken)
    {
        if (broken.Count > 0)
        {
            throw new InvalidOperationException(
                "Navigation requests that cannot be delivered:" + Environment.NewLine +
                string.Join(Environment.NewLine, broken.Select(entry => "  - " + entry)));
        }
    }
}
