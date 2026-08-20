using Avalonia.Controls;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Startup;
using CdCSharp.Pangea.Windows;

namespace CdCSharp.Pangea.Shell;

/// <summary>
/// The shell of a platform that has windows: the one it always was.
/// </summary>
internal sealed class DesktopShellPresenter : IShellPresenter
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IWindowManager _windowManager;

    public DesktopShellPresenter(IServiceProvider serviceProvider, IWindowManager windowManager)
    {
        _serviceProvider = serviceProvider;
        _windowManager = windowManager;
    }

    public bool IsSingleView => false;

    public void ShowMain()
    {
        _windowManager.Initialize();
        _windowManager.GetMainWindow()?.Show();
    }

    public Control? ShowSplash(PangeaStartupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.ShowSplash) return null;

        Window splash = Create(options);
        splash.Show();

        return splash;
    }

    public void HideSplash(Control? splash) => (splash as Window)?.Close();

    public Task<bool> ShowMessageAsync(MessageDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Window owner = _windowManager.GetMainWindow()
            ?? throw new InvalidOperationException(
                "A dialog needs an owner window, and no main window has been set. " +
                "Call SetMainWindow before showing a dialog.");

        MessageDialogWindow dialog = request.CancelText is null
            ? MessageDialogWindow.Statement(request.Title, request.Message, request.ConfirmText)
            : MessageDialogWindow.Question(request.Title, request.Message, request.ConfirmText, request.CancelText);

        // Closing a dialog by its window chrome produces no result at all, which is the same intent
        // as cancelling and is read as such.
        return Answer(dialog, owner);
    }

    private static async Task<bool> Answer(MessageDialogWindow dialog, Window owner) =>
        await dialog.ShowDialog<bool?>(owner) ?? false;

    private Window Create(PangeaStartupOptions options)
    {
        if (options.SplashWindowType is null) return new PangeaSplashWindow(options.SplashTitle);

        if (!typeof(Window).IsAssignableFrom(options.SplashWindowType))
        {
            throw new InvalidOperationException(
                $"'{options.SplashWindowType.FullName}' is configured as the splash window but does not derive from Window.");
        }

        // Through the container when it knows the type - a splash with a view model is still a
        // view - and by constructor otherwise.
        return (Window)(_serviceProvider.GetService(options.SplashWindowType)
                        ?? Activator.CreateInstance(options.SplashWindowType)
                        ?? throw new InvalidOperationException(
                            $"The splash window '{options.SplashWindowType.FullName}' could not be created."));
    }
}
