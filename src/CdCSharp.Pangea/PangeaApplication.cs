using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Services;
using CdCSharp.Pangea.Startup;
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
        IServiceProvider serviceProvider = GetServiceProvider();

        if (serviceProvider is not null)
        {
            IPangeaApplicationContext applicationContext = new PangeaApplicationContext(this, serviceProvider);
            serviceProvider.GetRequiredService<FeatureRegistry>().ConfigureApplication(serviceProvider, applicationContext);

            // A window belongs to a lifetime. Without one - a headless test session, a XAML
            // designer - there is nowhere to show it, and the features above have already done
            // the part of startup that does not depend on having somewhere.
            //
            // Started rather than awaited, and it must be: with initializers registered the work
            // finishes only after the splash has been on screen for a while, and the UI thread has
            // to be back in the message loop for that to happen at all. Start observes the failure
            // that an abandoned task would otherwise swallow.
            if (ApplicationLifetime is not null) StartupSequence.Start(serviceProvider);
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    public virtual PangeaOptions ConfigurePangeaOptions(PangeaOptions options) => options;
    public virtual void Configure(IServiceCollection services) { }

}