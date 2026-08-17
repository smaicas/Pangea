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
        TypeRegistry typeRegistry = serviceProvider.GetRequiredService<TypeRegistry>();
        VerifyRequestsReachTheirDestination(typeRegistry);

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

        if (broken.Count > 0)
        {
            throw new InvalidOperationException(
                "Navigation requests that cannot be delivered:" + Environment.NewLine +
                string.Join(Environment.NewLine, broken.Select(entry => "  - " + entry)));
        }
    }
}
