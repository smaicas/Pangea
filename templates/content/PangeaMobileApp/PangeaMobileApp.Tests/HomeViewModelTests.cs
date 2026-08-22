using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Testing;
using PangeaMobileApp.ViewModels;
using System.ComponentModel;

namespace PangeaMobileApp.Tests;

/// <summary>
/// The four conventions a Pangea screen is built from, one test each. Copy this file for your own
/// screens: the shape does not change, only the assertions do.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PangeaTestServices"/> is the container a <c>ViewModelBase</c> asks for its services,
/// with doubles in it: commands run inline, dialogs answer from a script, navigations are recorded
/// rather than performed. No Avalonia is started.
/// </para>
/// <para>
/// The command test is the one worth keeping. Most of what a generated view model does is visible
/// the moment you run the application; a command that never re-evaluates its CanExecute looks like
/// a button that is simply disabled, and that is a bug people stare at for an afternoon.
/// </para>
/// </remarks>
public class HomeViewModelTests
{
    [Fact]
    public void ABindingField_NotifiesUnderItsPropertyName()
    {
        PangeaTestServices services = new();
        HomeViewModel screen = new(services);

        List<string?> raised = [];
        ((INotifyPropertyChanged)screen).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        screen.Name = "Ada";

        Assert.Contains(nameof(HomeViewModel.Name), raised);
    }

    [Fact]
    public void AComputedProperty_IsNotifiedByWhatItReads()
    {
        PangeaTestServices services = new();
        HomeViewModel screen = new(services);

        List<string?> raised = [];
        ((INotifyPropertyChanged)screen).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        screen.Taps = 3;

        Assert.Contains(nameof(HomeViewModel.Greeting), raised);
    }

    [Fact]
    public void ACommand_ReEvaluatesWhenItsDependencyChanges()
    {
        PangeaTestServices services = new();
        HomeViewModel screen = new(services);

        // Read once and held, exactly as a binding does: the notification has to reach this
        // instance, not a fresh one built by the next read of the property.
        RelayCommand greet = screen.GreetCommand;

        int raised = 0;
        greet.CanExecuteChanged += (_, _) => raised++;

        Assert.False(greet.CanExecute(null));

        screen.Name = "Ada";

        Assert.True(raised >= 1, $"Setting Name notified the command {raised} time(s).");
        Assert.True(greet.CanExecute(null));
    }

    [Fact]
    public void AsyncCommand_RunsToCompletionAndShowsItsDialog()
    {
        PangeaTestServices services = new();
        HomeViewModel screen = new(services);

        // Inline dispatcher: the command's body has finished by the time Execute returns.
        screen.AboutCommand.Execute(null);

        Assert.Single(services.Dialogs.Alerts);
    }
}
