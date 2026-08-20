namespace CdCSharp.Pangea.Core.Abstractions;

/// <summary>
/// Something that lets go of what it subscribed to, when whoever created it is finished with it.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not <see cref="IDisposable"/>. Microsoft's container tracks every transient service
/// it creates that implements <see cref="IDisposable"/> and holds it until the provider itself is
/// disposed - which, for an application, is until the process ends. View models are transient by
/// default, so making them disposable would replace one leak with a larger and quieter one: every
/// screen ever opened, kept alive by the container that built it.
/// </para>
/// <para>
/// <c>ViewModelBase</c> implements this. The navigation service calls it for the view models it
/// drops; anything holding a view model outside navigation is responsible for calling it itself.
/// </para>
/// </remarks>
public interface IDiscardable
{
    /// <summary>
    /// Releases what this registered. Called more than once is not an error and does nothing after
    /// the first time.
    /// </summary>
    void Discard();
}
