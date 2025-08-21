using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Core.Services;
using CdCSharp.Pangea.Services;
using CdCSharp.Pangea.Theming;
using CdCSharp.Pangea.Theming.Abstractions;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
 
            #if DEBUG
            bool isValid = ServiceValidation.ValidateSameInstance(serviceProvider);
            if (!isValid)
            {
                throw new InvalidOperationException("❌ Static Services is NOT using the same ServiceProvider instance as PangeaApplication!");
            }
            System.Diagnostics.Debug.WriteLine("✅ ServiceProvider instance validation passed");
            #endif
            IPangeaApplicationContext applicationContext = new PangeaApplicationContext(Current!, serviceProvider);
            FeatureRegistry.ConfigureAllFeatures(serviceProvider, applicationContext);
            
            IThemeService? themeService = serviceProvider.GetRequiredService<IThemeService>();
            ThemingOptions? themingOptions = serviceProvider.GetRequiredService<IOptions<ThemingOptions>>().Value;

            PangeaUI toolkitUI = new(serviceProvider, themingOptions);

            if (Current?.Styles != null)
            {
                bool hasToolkitUI = Current.Styles.Any(s => s is PangeaUI);
                if (!hasToolkitUI)
                    Current.Styles.Add(toolkitUI);
            }

            themeService?.RegisterToolkitUI(toolkitUI);
            
            IWindowManager? windowManager = serviceProvider.GetService<IWindowManager>();
            windowManager?.GetMainWindow().Show();
        }

        base.OnFrameworkInitializationCompleted();
    }
    
    
    public virtual PangeaOptions ConfigurePangeaOptions(PangeaOptions options) => options;
    public virtual void Configure(IServiceCollection services) { }
}