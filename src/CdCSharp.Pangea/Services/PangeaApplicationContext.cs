using Avalonia.Styling;
using CdCSharp.Pangea.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CdCSharp.Pangea.Services;

internal class PangeaApplicationContext : IPangeaApplicationContext
{
    private readonly global::Avalonia.Application _application;
    private readonly IServiceProvider _serviceProvider;

    public PangeaApplicationContext(global::Avalonia.Application application, IServiceProvider serviceProvider)
    {
        _application = application;
        _serviceProvider = serviceProvider;
    }

    public void AddStyle(object style)
    {
        if (style is IStyle avaloniaStyle)
            _application.Styles.Add(avaloniaStyle);
    }

    public void RemoveStyle(object style)
    {
        if (style is IStyle avaloniaStyle)
            _application.Styles.Remove(avaloniaStyle);
    }

    public bool HasStyle<T>() where T : class
    {
        return _application.Styles.Any(s => s is T);
    }

    public T? GetRequiredService<T>() where T : class
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    public object? GetApplication()
    {
        return _application;
    }
}