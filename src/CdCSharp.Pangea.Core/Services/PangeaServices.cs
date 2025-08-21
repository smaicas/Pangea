using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Core.Services;

public static class PangeaServices
{
    private static IServiceProvider? _serviceProvider;
    
    public static T GetRequiredService<T>() where T : class
    {
        if (_serviceProvider == null)
            throw new InvalidOperationException("Pangea ServiceProvider not initialized. Make sure your application inherits from PangeaApplication.");
        
        return _serviceProvider.GetRequiredService<T>();
    }
    
    public static void Initialize(IServiceProvider serviceProvider)
    {
        if (serviceProvider == null)
            throw new ArgumentNullException(nameof(serviceProvider));

        if (_serviceProvider != null)
            throw new InvalidOperationException("Services already initialized. ServiceProvider can only be initialized once.");

        _serviceProvider = serviceProvider;
        System.Diagnostics.Debug.WriteLine("✅ Static Services initialized with Pangea ServiceProvider");
    }

    internal static bool IsInitialized => _serviceProvider != null;
    
    internal static IServiceProvider Provider
    {
        get
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("Pangea ServiceProvider not initialized.");
            return _serviceProvider;
        }
    }
}

public static class ServiceValidation
{
    public static bool ValidateSameInstance(IServiceProvider pangeaServiceProvider)
    {
        if (!PangeaServices.IsInitialized)
            return false;
        bool isSameInstance = ReferenceEquals(PangeaServices.Provider, pangeaServiceProvider);
        
        System.Diagnostics.Debug.WriteLine($"ServiceProvider instance validation: {(isSameInstance ? "✅ SAME" : "❌ DIFFERENT")}");
        try
        {
            IRelayCommandFactory service1 = pangeaServiceProvider.GetRequiredService<IRelayCommandFactory>();
            IRelayCommandFactory service2 = PangeaServices.GetRequiredService<IRelayCommandFactory>();
            
            bool sameService = ReferenceEquals(service1, service2);
            System.Diagnostics.Debug.WriteLine($"Service resolution validation: {(sameService ? "✅ SAME" : "❌ DIFFERENT")}");
            
            return isSameInstance && sameService;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Service validation error: {ex.Message}");
            return false;
        }
    }
}
