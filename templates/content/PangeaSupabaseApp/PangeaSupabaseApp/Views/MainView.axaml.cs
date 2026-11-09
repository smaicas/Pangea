using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace PangeaSupabaseApp.Views;

/// <summary>
/// The shell, as a control.
/// </summary>
/// <remarks>
/// This is what Android and iOS are given directly - they have no windows to put it in - and what
/// the desktop head's <see cref="MainWindow"/> wraps. One shell, three platforms.
/// </remarks>
public partial class MainView : UserControl
{
    private TopLevel? _topLevel;

    public MainView() => InitializeComponent();

    /// <summary>
    /// Keeps the shell out from under the status bar, the notch and the gesture handle.
    /// </summary>
    /// <remarks>
    /// Android 15 forces every application edge to edge, so the view is handed the whole screen and
    /// the platform's own answer for where it is safe to draw is the only one worth having: it
    /// moves with rotation, with a keyboard, and with whatever shape the next phone's cut-out is.
    /// </remarks>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is null) return;

        _topLevel.ScalingChanged += OnScalingChanged;

        if (_topLevel.InsetsManager is not { } insets) return;

        Apply(insets.SafeAreaPadding);

        insets.SafeAreaChanged += OnSafeAreaChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null)
        {
            _topLevel.ScalingChanged -= OnScalingChanged;

            if (_topLevel.InsetsManager is { } insets) insets.SafeAreaChanged -= OnSafeAreaChanged;

            _topLevel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e) => Apply(e.SafeAreaPadding);

    /// <summary>
    /// Reads the safe area again once the surface knows its density.
    /// </summary>
    /// <remarks>
    /// The first report arrives while the surface is still 1x1 with a scaling of 1, so it is the
    /// platform's physical pixels taken for device-independent ones: nearly three times too big on
    /// a typical phone. The insets themselves do not change when the real scaling arrives, so
    /// <c>SafeAreaChanged</c> never corrects it.
    /// </remarks>
    private void OnScalingChanged(object? sender, EventArgs e)
    {
        if (_topLevel?.InsetsManager is { } insets) Apply(insets.SafeAreaPadding);
    }

    private void Apply(Thickness safeArea) => Shell.Margin = safeArea;
}
