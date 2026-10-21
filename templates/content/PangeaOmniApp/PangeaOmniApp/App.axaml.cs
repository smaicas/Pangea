using Avalonia.Markup.Xaml;
using CdCSharp.Pangea;
using CdCSharp.Pangea.Storage;
using Microsoft.Extensions.DependencyInjection;
using PangeaOmniApp.Services;

namespace PangeaOmniApp;

public partial class App : PangeaApplication
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void Configure(IServiceCollection services)
    {
        // The data folder keeps the application's name, so it is the same however it is started:
        // by the host, from an IDE, or on its own.
        services.Configure<StorageOptions>(options => options.ApplicationName = "PangeaOmniApp");

        services.AddSingleton<GreetingLog>();

        // Nothing to register for the bus: the request handlers are found by the generator, and
        // IAppBus and IAppHostContext come with CdCSharp.Pangea.Apps.
    }
}
