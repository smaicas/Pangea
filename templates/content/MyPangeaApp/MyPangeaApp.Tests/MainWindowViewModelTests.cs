using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Testing;
using MyPangeaApp.ViewModels;
using System.ComponentModel;

namespace MyPangeaApp.Tests;

/// <summary>
/// The three conventions the screen shows, one test each. Copy this file for your own screens: the
/// shape does not change, only the assertions do.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PangeaTestServices"/> is the container a <c>ViewModelBase</c> asks for its services,
/// with doubles in it: commands run inline, dialogs answer from a script, navigations are recorded
/// rather than performed. Nothing here starts Avalonia, so the whole file runs in milliseconds.
/// </para>
/// <para>
/// The command test is the one worth keeping. Most of what a generated view model does is visible
/// the moment you run the application; a command that never re-evaluates its CanExecute looks like
/// a button that is simply disabled, and that is a bug people stare at for an afternoon.
/// </para>
/// </remarks>
public class MainWindowViewModelTests
{
    [Fact]
    public void ABindingField_NotifiesUnderItsPropertyName()
    {
        MainWindowViewModel screen = new(new PangeaTestServices());

        List<string?> raised = [];
        ((INotifyPropertyChanged)screen).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        screen.Name = "Ada";

        Assert.Contains(nameof(MainWindowViewModel.Name), raised);
    }

    [Fact]
    public void AComputedProperty_IsNotifiedByWhatItReads()
    {
        MainWindowViewModel screen = new(new PangeaTestServices());

        List<string?> raised = [];
        ((INotifyPropertyChanged)screen).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        screen.Name = "Ada";

        Assert.Contains(nameof(MainWindowViewModel.Greeting), raised);
        Assert.Equal("Hello, Ada!", screen.Greeting);
    }

    [Fact]
    public void ACommand_ReEvaluatesWhenItsDependencyChanges()
    {
        MainWindowViewModel screen = new(new PangeaTestServices()) { Name = "" };

        // Read once and held, exactly as a binding does: the notification has to reach this
        // instance, not a fresh one built by the next read of the property.
        RelayCommand greet = screen.GreetCommand;

        int raised = 0;
        greet.CanExecuteChanged += (_, _) => raised++;

        Assert.False(greet.CanExecute(null));

        screen.Name = "Ada";

        Assert.True(raised >= 1, $"Setting Name notified the command {raised} time(s).");
        Assert.True(greet.CanExecute(null));

        greet.Execute(null);

        Assert.Equal(1, screen.Clicks);
    }
}
