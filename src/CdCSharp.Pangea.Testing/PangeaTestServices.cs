using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Navigation.Abstractions;
using CdCSharp.Pangea.Storage.Abstractions;
using CdCSharp.Pangea.Testing.Dispatchers;
using CdCSharp.Pangea.Testing.Fakes;
using CdCSharp.Pangea.Theming.Abstractions;

namespace CdCSharp.Pangea.Testing;

/// <summary>
/// The container a view model needs, without an application around it.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="ViewModelBase"/> takes an <see cref="IServiceProvider"/> and asks it for whatever
/// it needs, so testing one otherwise means starting Avalonia, building the real container and
/// waiting for a window. This is the same shape with test doubles in it:
/// </para>
/// <code>
/// PangeaTestServices services = new();
/// OrderViewModel screen = new(services);
///
/// screen.OpenCommand.Execute(null);
///
/// Assert.Equal(typeof(OrderDetailViewModel), services.Navigation.LastDestination);
/// </code>
/// <para>
/// Commands run inline, dialogs answer from a script, and navigations are recorded rather than
/// performed. Add the application's own services with <see cref="Add{TService}"/>.
/// </para>
/// </remarks>
public sealed class PangeaTestServices : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = [];

    /// <param name="dispatcher">
    /// The dispatcher commands are built with. Defaults to <see cref="InlineUIDispatcher"/>, which
    /// runs everything where it was called; pass a <see cref="PumpingUIDispatcher"/> when a test is
    /// about work reaching the UI thread.
    /// </param>
    public PangeaTestServices(IUIDispatcher? dispatcher = null)
    {
        Dispatcher = dispatcher ?? new InlineUIDispatcher();
        Dialogs = new RecordingDialogService();
        Navigation = new RecordingNavigationService();
        Storage = new InMemoryStorageService();
        Theming = new RecordingThemeService();

        Add<IUIDispatcher>(Dispatcher);
        Add<IRelayCommandFactory>(new RelayCommandFactory(Dispatcher));
        Add<IDialogService>(Dialogs);
        Add<INavigationService>(Navigation);
        Add<IStorageService>(Storage);
        Add<IThemeService>(Theming);
    }

    public IUIDispatcher Dispatcher { get; }

    /// <summary>The dialog service view models will be handed. Script it before exercising them.</summary>
    public RecordingDialogService Dialogs { get; }

    /// <summary>The navigation service view models will be handed. Assert on it afterwards.</summary>
    public RecordingNavigationService Navigation { get; }

    /// <summary>Storage that keeps everything in memory, so nothing is left on disk.</summary>
    public InMemoryStorageService Storage { get; }

    /// <summary>The theme service view models will be handed. Records what was asked for.</summary>
    public RecordingThemeService Theming { get; }

    /// <summary>
    /// Registers <paramref name="instance"/> under <typeparamref name="TService"/>, replacing
    /// whatever was there. Returns this, so registrations chain.
    /// </summary>
    public PangeaTestServices Add<TService>(TService instance) where TService : notnull
    {
        _services[typeof(TService)] = instance;
        return this;
    }

    /// <summary>
    /// What was registered, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Null rather than an exception, because that is what <see cref="IServiceProvider"/> promises
    /// and what <c>GetRequiredService</c> turns into a message naming the missing type - which
    /// reads better than anything this could throw.
    /// </remarks>
    public object? GetService(Type serviceType) =>
        _services.TryGetValue(serviceType, out object? service) ? service : null;
}
