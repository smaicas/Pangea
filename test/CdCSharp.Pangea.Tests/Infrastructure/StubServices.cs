using Avalonia.Controls.ApplicationLifetimes;

namespace CdCSharp.Pangea.Tests.Infrastructure;

/// <summary>
/// Resolves view models the way the container would, by constructing them, and hands out the
/// application lifetime the window manager asks for when it attaches a main window.
/// </summary>
/// <remarks>
/// The lifetime can be supplied so a test can assert what the window manager published to it.
/// </remarks>
internal sealed class StubServices : IServiceProvider
{
    private readonly IApplicationLifetime _lifetime;

    public StubServices(IApplicationLifetime? lifetime = null) =>
        _lifetime = lifetime ?? new ClassicDesktopStyleApplicationLifetime();

    public object? GetService(Type serviceType) =>
        serviceType == typeof(IApplicationLifetime)
            ? _lifetime
            : Activator.CreateInstance(serviceType);
}
