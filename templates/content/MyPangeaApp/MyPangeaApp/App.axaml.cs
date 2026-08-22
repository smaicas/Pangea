using Avalonia.Markup.Xaml;
using CdCSharp.Pangea;
using Microsoft.Extensions.DependencyInjection;

namespace MyPangeaApp;

public partial class App : PangeaApplication
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void Configure(IServiceCollection services)
    {
        // Register your own services here. View models deriving from ViewModelBase are
        // registered automatically.
    }
}
