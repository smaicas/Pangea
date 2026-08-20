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
