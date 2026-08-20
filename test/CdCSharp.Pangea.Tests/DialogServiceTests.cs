using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Core.Configuration;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Shell;
using CdCSharp.Pangea.Testing.Dispatchers;
using CdCSharp.Pangea.Tests.Infrastructure;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CdCSharp.Pangea.Tests;

/// <summary>
/// Asking the user a question without writing a window for it.
/// </summary>
/// <remarks>
/// Driven the way a person drives it: the dialog is caught as it is built and one of its buttons is
/// pressed. A modal dialog does not return until something closes it, so every wait here is bounded
/// - a test that spins until an answer arrives hangs the suite when the answer never comes.
/// </remarks>
public class DialogServiceTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private static WindowManager BuildWindows(PumpingUIDispatcher dispatcher) =>
        new(new StubServices(),
            Options.Create(new PangeaOptions { Window = { AutoDiscoverMainWindow = false } }),
            new TypeRegistry(),
            dispatcher,
            NullLogger<WindowManager>.Instance);

    private static (DialogService Dialogs, Window Owner) Arrange()
    {
        PumpingUIDispatcher dispatcher = new();
        WindowManager windows = BuildWindows(dispatcher);

        Window owner = new();
        windows.SetMainWindow(owner);
        owner.Show();

        return (DialogService.For(new DesktopShellPresenter(new StubServices(), windows), dispatcher), owner);
    }

    /// <summary>
    /// Runs the ask, answers it through <paramref name="respond"/>, and returns what the caller got.
    /// </summary>
    private static (bool Completed, MessageDialogWindow? Dialog) Ask(
        Func<Task> start, Action<MessageDialogWindow> respond, out Task pending)
    {
        MessageDialogWindow? seen = null;

        void OnCreated(MessageDialogWindow dialog) => seen = dialog;

        MessageDialogWindow.Created += OnCreated;

        try
        {
            pending = start();

            DateTime deadline = DateTime.UtcNow + Patience;
            bool answered = false;

            while (DateTime.UtcNow < deadline && !pending.IsCompleted)
            {
                Dispatcher.UIThread.RunJobs();

                if (!answered && seen is not null)
                {
                    respond(seen);
                    answered = true;
                }

                Thread.Sleep(1);
            }

            return (pending.IsCompleted, seen);
        }
        finally
        {
            MessageDialogWindow.Created -= OnCreated;
        }
    }

    [AvaloniaFact]
    public void Confirm_ReturnsTrueWhenTheUserConfirms()
    {
        (DialogService dialogs, Window owner) = Arrange();

        (bool completed, _) = Ask(
            () => dialogs.ConfirmAsync("Delete", "Delete the order?"),
            dialog => dialog.Close(true),
            out Task pending);

        Assert.True(completed, "The dialog never answered.");
        Assert.True(((Task<bool>)pending).Result);
        owner.Close();
    }

    [AvaloniaFact]
    public void Confirm_ReturnsFalseWhenTheUserCancels()
    {
        (DialogService dialogs, Window owner) = Arrange();

        (bool completed, _) = Ask(
            () => dialogs.ConfirmAsync("Delete", "Delete the order?"),
            dialog => dialog.Close(false),
            out Task pending);

        Assert.True(completed, "The dialog never answered.");
        Assert.False(((Task<bool>)pending).Result);
        owner.Close();
    }

    /// <summary>
    /// Dismissing by the window chrome produces no result at all, which is the same intent as
    /// cancelling and must never read as confirmation.
    /// </summary>
    [AvaloniaFact]
    public void Confirm_TreatsBeingDismissedAsACancel()
    {
        (DialogService dialogs, Window owner) = Arrange();

        (bool completed, _) = Ask(
            () => dialogs.ConfirmAsync("Delete", "Delete the order?"),
            dialog => dialog.Close(),
            out Task pending);

        Assert.True(completed, "The dialog never answered.");
        Assert.False(((Task<bool>)pending).Result);
        owner.Close();
    }

    [AvaloniaFact]
    public void Alert_HasNothingToCancel()
    {
        (DialogService dialogs, Window owner) = Arrange();

        (bool completed, MessageDialogWindow? dialog) = Ask(
            () => dialogs.AlertAsync("Saved", "Your changes are saved."),
            shown => shown.Close(true),
            out _);

        Assert.True(completed, "The dialog never closed.");
        Assert.NotNull(dialog);
        Assert.Null(dialog!.CancelButton);
        owner.Close();
    }

    [AvaloniaFact]
    public void TheButtonsSayWhatTheCallerAskedThemToSay()
    {
        (DialogService dialogs, Window owner) = Arrange();

        (_, MessageDialogWindow? dialog) = Ask(
            () => dialogs.ConfirmAsync("Delete", "Sure?", "Delete it", "Keep it"),
            shown => shown.Close(false),
            out _);

        Assert.NotNull(dialog);
        Assert.Equal("Delete it", dialog!.ConfirmButton.Content);
        Assert.Equal("Keep it", dialog.CancelButton!.Content);
        owner.Close();
    }

    /// <summary>
    /// A dialog is not a window of its own in any sense the user cares about.
    /// </summary>
    /// <remarks>
    /// Minimising one would hide it while it still blocked the window it came from, and a taskbar
    /// entry suggests something you can switch to. The close button is deliberately kept: being
    /// dismissed is a real answer, and a dialog with a single way out is a worse dialog.
    /// </remarks>
    [AvaloniaFact]
    public void TheDialogDoesNotPretendToBeAnOrdinaryWindow()
    {
        (DialogService dialogs, Window owner) = Arrange();

        (_, MessageDialogWindow? dialog) = Ask(
            () => dialogs.ConfirmAsync("Delete", "Sure?"),
            shown => shown.Close(false),
            out _);

        Assert.NotNull(dialog);
        Assert.False(dialog!.CanResize);
        Assert.False(dialog.CanMaximize);
        Assert.False(dialog.CanMinimize);
        Assert.False(dialog.ShowInTaskbar);
        owner.Close();
    }

    /// <summary>Presses a key once the dialog is actually open, and reports what the caller got.</summary>
    private static (bool Completed, bool Result, object? Focused) Press(
        Func<Task<bool>> start, PhysicalKey key)
    {
        MessageDialogWindow? seen = null;
        bool opened = false;

        void OnCreated(MessageDialogWindow dialog)
        {
            seen = dialog;
            dialog.Opened += (_, _) => opened = true;
        }

        MessageDialogWindow.Created += OnCreated;

        try
        {
            Task<bool> pending = start();

            DateTime deadline = DateTime.UtcNow + Patience;
            bool pressed = false;
            object? focused = null;

            while (DateTime.UtcNow < deadline && !pending.IsCompleted)
            {
                Dispatcher.UIThread.RunJobs();

                // Only once it is open: a control cannot hold focus before the window exists.
                if (!pressed && opened && seen is not null)
                {
                    focused = seen.FocusManager?.GetFocusedElement();
                    seen.KeyPressQwerty(key, RawInputModifiers.None);
                    pressed = true;
                }

                Thread.Sleep(1);
            }

            if (!pending.IsCompleted) seen?.Close(false);

            while (!pending.IsCompleted)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(1);
            }

            return (pressed && pending.IsCompleted, pending.Result, focused);
        }
        finally
        {
            MessageDialogWindow.Created -= OnCreated;
        }
    }

    /// <summary>
    /// Escape is how a dialog is refused without reaching for the mouse, and it answers the same as
    /// the cancel button.
    /// </summary>
    [AvaloniaFact]
    public void Escape_Cancels()
    {
        (DialogService dialogs, Window owner) = Arrange();

        (bool completed, bool result, _) = Press(
            () => dialogs.ConfirmAsync("Delete", "Delete the order?"), PhysicalKey.Escape);

        Assert.True(completed, "Escape did not close the dialog.");
        Assert.False(result);
        owner.Close();
    }

    [AvaloniaFact]
    public void Enter_Confirms()
    {
        (DialogService dialogs, Window owner) = Arrange();

        (bool completed, bool result, _) = Press(
            () => dialogs.ConfirmAsync("Delete", "Delete the order?"), PhysicalKey.Enter);

        Assert.True(completed, "Enter did not close the dialog.");
        Assert.True(result);
        owner.Close();
    }

    /// <summary>
    /// Enter and Escape are routed by IsDefault and IsCancel whatever holds focus, but Space acts on
    /// the focused control and Tab needs somewhere to start. A dialog that focuses nothing is one
    /// the keyboard cannot reach.
    /// </summary>
    /// <remarks>
    /// Focus is asserted rather than Space itself: the headless backend does not deliver Space to
    /// the focused button, though a real window does - confirmed by hand in the gallery. A test for
    /// it here could only ever fail, so what is pinned is the condition Space depends on.
    /// </remarks>
    [AvaloniaFact]
    public void TheDialogFocusesAButtonWhenItOpens()
    {
        (DialogService dialogs, Window owner) = Arrange();

        (_, _, object? focused) = Press(
            () => dialogs.ConfirmAsync("Delete", "Delete the order?"), PhysicalKey.Escape);

        Button button = Assert.IsType<Button>(focused);
        Assert.Equal("OK", button.Content);
        owner.Close();
    }

    /// <summary>
    /// A long message must not push the buttons off the screen.
    /// </summary>
    /// <remarks>
    /// The dialog sizes itself to its content and cannot be resized, so without a cap a long enough
    /// message grew it past the screen: no scroll, no resize, and the buttons somewhere unreachable.
    /// A dialog nobody can answer is worse than an ugly one.
    /// </remarks>
    [AvaloniaFact]
    public void ALongMessage_ScrollsInsteadOfGrowingWithoutEnd()
    {
        (DialogService dialogs, Window owner) = Arrange();

        string wall = string.Join(" ", Enumerable.Repeat("This message goes on and on.", 200));

        (_, MessageDialogWindow? dialog) = Ask(
            () => dialogs.ConfirmAsync("Long", wall),
            shown => shown.Close(false),
            out _);

        Assert.NotNull(dialog);
        Assert.True(dialog!.Height <= dialog.MaxHeight,
            $"The dialog grew to {dialog.Height}, past its cap of {dialog.MaxHeight}.");

        // The message is what scrolls, and the buttons sit outside it.
        Assert.NotNull(dialog.MessageScroller);
        Assert.DoesNotContain(dialog.ConfirmButton,
            dialog.MessageScroller.GetVisualDescendants().OfType<Button>());

        owner.Close();
    }

    [AvaloniaFact]
    public async Task WithoutAMainWindow_TheDialogSaysWhatIsMissing()
    {
        PumpingUIDispatcher dispatcher = new();
        DialogService dialogs = DialogService.For(
            new DesktopShellPresenter(new StubServices(), BuildWindows(dispatcher)), dispatcher);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dialogs.ConfirmAsync("Delete", "Delete the order?"));

        Assert.Contains("SetMainWindow", error.Message, StringComparison.Ordinal);
    }
}
