namespace CdCSharp.Pangea.Core.Abstractions;

/// <summary>
/// Work that has to finish before the application's main window is shown.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IPangeaFeature.ConfigureApplication"/> runs on the UI thread and returns nothing, so
/// the only thing a feature can do with slow work there is start it and hope. That is right for
/// work whose result merely replaces a default - the shell template restores the saved culture
/// that way - and wrong for work the first screen cannot do without. A database that is one
/// migration behind is not a detail the first view model can paper over: it either ran or the
/// application has nothing to show.
/// </para>
/// <para>
/// Every registered initializer is awaited, in <see cref="Order"/>, while a splash window stands in
/// for the main one. Registering none leaves startup exactly as it was: the main window is created
/// and shown with nothing in between.
/// </para>
/// <para>
/// Initializers run off the UI thread. Anything that touches a control or a view model has to come
/// back through <see cref="IUIDispatcher"/> first.
/// </para>
/// </remarks>
public interface IPangeaAsyncInitializer
{
    /// <summary>What this is doing, shown on the splash window while it runs.</summary>
    string Name { get; }

    /// <summary>Lower runs first. Initializers with the same order run in registration order.</summary>
    int Order => 0;

    /// <summary>
    /// Runs the work. Throwing aborts startup: see <c>PangeaStartupOptions.FailureBehavior</c> for
    /// what the application does about it.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken);
}
