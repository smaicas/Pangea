using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Services;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea;

public abstract class PangeaApplication : Application
{
    public static readonly AttachedProperty<IServiceProvider> ServiceProviderProperty =
        AvaloniaProperty.RegisterAttached<Application, IServiceProvider>("ServiceProvider", typeof(PangeaApplication));
    
    public IServiceProvider GetServiceProvider() => GetValue(ServiceProviderProperty);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime != null)
        {
            IServiceProvider serviceProvider = GetServiceProvider();

            IPangeaApplicationContext applicationContext = new PangeaApplicationContext(Current!, serviceProvider);
            serviceProvider.GetRequiredService<FeatureRegistry>().ConfigureApplication(serviceProvider, applicationContext);
        
            // Main window creation and display
            IWindowManager? windowManager = serviceProvider.GetService<IWindowManager>();
            if (windowManager != null)
            {
                windowManager.Initialize();
                windowManager.GetMainWindow()?.Show();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    public virtual PangeaOptions ConfigurePangeaOptions(PangeaOptions options) => options;
    public virtual void Configure(IServiceCollection services) { }

}