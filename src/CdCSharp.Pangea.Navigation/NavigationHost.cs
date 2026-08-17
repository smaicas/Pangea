using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CdCSharp.Pangea.Navigation.Abstractions;
using System.ComponentModel;

namespace CdCSharp.Pangea.Navigation;

/// <summary>
/// Displays whatever the navigation service currently points at. Drop one in a window and the
/// application navigates.
/// </summary>
/// <remarks>
/// A control built by XAML gets no constructor injection, so the feature publishes the services as
/// attached properties on the application - the same mechanism the toolkit already uses to publish
/// its container - and the host reads them from there. Setting <see cref="Service"/> or
/// <see cref="Locator"/> explicitly overrides that, which is what tests do.
/// </remarks>
public class NavigationHost : ContentControl
{
    public static readonly AttachedProperty<INavigationService?> ApplicationServiceProperty =
        AvaloniaProperty.RegisterAttached<Application, INavigationService?>(
            "ApplicationNavigationService", typeof(NavigationHost));

    public static readonly AttachedProperty<IViewLocator?> ApplicationLocatorProperty =
        AvaloniaProperty.RegisterAttached<Application, IViewLocator?>(
            "ApplicationViewLocator", typeof(NavigationHost));

    public static readonly StyledProperty<INavigationService?> ServiceProperty =
        AvaloniaProperty.Register<NavigationHost, INavigationService?>(nameof(Service));

    public static readonly StyledProperty<IViewLocator?> LocatorProperty =
        AvaloniaProperty.Register<NavigationHost, IViewLocator?>(nameof(Locator));

    /// <summary>
    /// Whether arriving at a screen moves keyboard focus into it. On by default.
    /// </summary>
    /// <remarks>
    /// Without it a navigation strands anyone not using a mouse: the element they were on is
    /// removed from the tree and nothing takes over, so the next Tab starts from the top of the
    /// window. Turn it off for a host that is not the main subject of the screen - a detail pane beside
    /// a list, where taking focus off the list is the wrong thing to do.
    /// </remarks>
    public static readonly StyledProperty<bool> MovesFocusOnNavigationProperty =
        AvaloniaProperty.Register<NavigationHost, bool>(nameof(MovesFocusOnNavigation), defaultValue: true);

    private INavigationService? _subscribed;
    private bool _attached;

    /// <summary>The service driving this host. Falls back to the one published by the feature.</summary>
    public INavigationService? Service
    {
        get => GetValue(ServiceProperty);
        set => SetValue(ServiceProperty, value);
    }

    /// <summary>The locator turning view models into views. Falls back to the one published by the feature.</summary>
    public IViewLocator? Locator
    {
        get => GetValue(LocatorProperty);
        set => SetValue(LocatorProperty, value);
    }

    /// <inheritdoc cref="MovesFocusOnNavigationProperty"/>
    public bool MovesFocusOnNavigation
    {
        get => GetValue(MovesFocusOnNavigationProperty);
        set => SetValue(MovesFocusOnNavigationProperty, value);
    }

    public static void SetApplicationService(Application application, INavigationService? value) =>
        application.SetValue(ApplicationServiceProperty, value);

    public static INavigationService? GetApplicationService(Application application) =>
        application.GetValue(ApplicationServiceProperty);

    public static void SetApplicationLocator(Application application, IViewLocator? value) =>
        application.SetValue(ApplicationLocatorProperty, value);

    public static IViewLocator? GetApplicationLocator(Application application) =>
        application.GetValue(ApplicationLocatorProperty);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Subscribe();
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        Unsubscribe();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ServiceProperty)
        {
            Subscribe();
            Refresh();
        }
        else if (change.Property == LocatorProperty)
        {
            Refresh();
        }
    }

    private INavigationService? ResolveService() =>
        Service ?? (Application.Current is { } application ? GetApplicationService(application) : null);

    private IViewLocator? ResolveLocator() =>
        Locator ?? (Application.Current is { } application ? GetApplicationLocator(application) : null);

    /// <summary>
    /// Only while attached, and never twice. Both the Service property and attaching to the tree
    /// lead here, so subscribing blindly leaves a second handler that outlives the window and keeps
    /// rebuilding content nobody can see.
    /// </summary>
    private void Subscribe()
    {
        Unsubscribe();

        if (!_attached) return;

        if (ResolveService() is not { } service) return;

        service.PropertyChanged += OnServiceChanged;
        _subscribed = service;
    }

    private void Unsubscribe()
    {
        if (_subscribed is null) return;

        _subscribed.PropertyChanged -= OnServiceChanged;
        _subscribed = null;
    }

    /// <summary>
    /// Puts keyboard focus on the first thing the user can act on in the screen just shown.
    /// </summary>
    /// <remarks>
    /// Queued rather than done inline: the content has only just been assigned and has no visual
    /// children to search until a layout pass has run. Falls back to the host itself so there is
    /// always somewhere for focus to land, and therefore somewhere for Tab to start.
    /// </remarks>
    private void MoveFocusIntoTheNewScreen()
    {
        if (!MovesFocusOnNavigation || !_attached) return;

        Dispatcher.UIThread.Post(() =>
        {
            if (!_attached || Content is not Visual shown) return;

            InputElement? target = shown.GetVisualDescendants()
                .OfType<InputElement>()
                .FirstOrDefault(candidate =>
                    candidate.Focusable && candidate.IsEffectivelyEnabled && candidate.IsEffectivelyVisible);

            if (target is not null)
            {
                target.Focus();
                return;
            }

            Focusable = true;
            Focus();
        }, DispatcherPriority.Loaded);
    }

    private void OnServiceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(INavigationService.CurrentViewModel))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        object? viewModel = ResolveService()?.CurrentViewModel;

        if (viewModel is null)
        {
            Content = null;
            return;
        }

        IViewLocator? locator = ResolveLocator();

        if (locator is null)
        {
            // Still being configured: XAML and object initializers assign one property at a time,
            // so a host that has its service but not yet its locator is half-built, not broken.
            // Attaching to the visual tree refreshes again, and by then it has to be there.
            if (!_attached)
            {
                Content = null;
                return;
            }

            throw new InvalidOperationException(
                "NavigationHost has no view locator. Add the navigation feature, or set Locator explicitly.");
        }

        Content = locator.Locate(viewModel);

        MoveFocusIntoTheNewScreen();
    }
}
