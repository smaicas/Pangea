using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Localization.Abstractions;

namespace CdCSharp.Pangea.Localization.Tests.Infrastructure;

/// <summary>
/// The two services a view model in this package asks for, and nothing else.
/// </summary>
/// <remarks>
/// Deliberately not <c>CdCSharp.Pangea.Testing</c>: that package pulls in the whole toolkit, and
/// these tests exist to prove the localization feature stands on its own.
/// </remarks>
internal sealed class TestServices : IServiceProvider
{
    private readonly RelayCommandFactory _commands = new(dispatcher: null);
    private readonly ILocalizationService _localization;

    public TestServices(ILocalizationService localization) => _localization = localization;

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IRelayCommandFactory)) return _commands;
        if (serviceType == typeof(ILocalizationService)) return _localization;

        return null;
    }
}
