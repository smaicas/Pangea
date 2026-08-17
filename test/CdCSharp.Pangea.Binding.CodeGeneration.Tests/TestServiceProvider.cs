using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;

namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// Minimal container satisfying <see cref="ViewModelBase"/>'s only dependency, so generated
/// ViewModels can be instantiated in tests without pulling in a full DI container.
/// </summary>
internal sealed class TestServiceProvider : IServiceProvider
{
    private readonly RelayCommandFactory _commandFactory = new(dispatcher: null);

    public object? GetService(Type serviceType) =>
        serviceType == typeof(IRelayCommandFactory) ? _commandFactory : null;
}
