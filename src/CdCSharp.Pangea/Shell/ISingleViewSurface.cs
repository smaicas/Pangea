using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace CdCSharp.Pangea.Shell;

/// <summary>
/// The one view a platform without windows will show, and the only thing the shell needs from its
/// application lifetime.
/// </summary>
/// <remarks>
/// A seam rather than a wrapper for its own sake: <see cref="ISingleViewApplicationLifetime"/>
/// cannot be implemented outside Avalonia, so a shell that depended on it directly could only be
/// exercised on a device. This can be stood in for, which is what makes the layering of the splash
/// and of every dialog testable at all.
/// </remarks>
public interface ISingleViewSurface
{
    /// <summary>What the platform is showing.</summary>
    Control? MainView { get; set; }
}

/// <summary>The real one, over the lifetime Avalonia handed the application.</summary>
internal sealed class LifetimeSingleViewSurface : ISingleViewSurface
{
    private readonly ISingleViewApplicationLifetime _lifetime;

    public LifetimeSingleViewSurface(ISingleViewApplicationLifetime lifetime) => _lifetime = lifetime;

    public Control? MainView
    {
        get => _lifetime.MainView;
        set => _lifetime.MainView = value;
    }
}

/// <summary>
/// Android's: each activity asks for the view it is to show.
/// </summary>
/// <remarks>
/// <para>
/// This is the route Avalonia wants on Android, where assigning
/// <see cref="ISingleViewApplicationLifetime.MainView"/> is reported as not fully supported, and
/// after which that property stays <see langword="null"/>: what needs the view asks this instead.
/// </para>
/// <para>
/// A factory would usually build a new view each time. The shell cannot be rebuilt: it holds the
/// navigation history and any dialog that is up, which belong to the application and not to the
/// activity. So every activity gets the same one, and an activity that arrives while the previous
/// one is still alive takes it over, where a view with a parent already would throw.
/// </para>
/// </remarks>
internal sealed class ActivitySingleViewSurface : ISingleViewSurface
{
    private readonly Action<Func<Control>?> _setFactory;

    private Control? _mainView;

    public ActivitySingleViewSurface(IActivityApplicationLifetime lifetime)
        : this(factory => lifetime.MainViewFactory = factory)
    {
    }

    /// <summary>The factory's destination alone, because the lifetime cannot be stood in for.</summary>
    internal ActivitySingleViewSurface(Action<Func<Control>?> setFactory) => _setFactory = setFactory;

    public Control? MainView
    {
        get => _mainView;
        set
        {
            _mainView = value;
            _setFactory(value is null ? null : TakeView);
        }
    }

    private Control TakeView()
    {
        Control view = _mainView!;

        if (view.Parent is ContentControl previous) previous.Content = null;

        return view;
    }
}
