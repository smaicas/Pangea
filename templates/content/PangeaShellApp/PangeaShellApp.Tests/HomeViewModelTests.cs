using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Testing;
using PangeaShellApp.Navigation;
using PangeaShellApp.ViewModels;
using System.ComponentModel;

namespace PangeaShellApp.Tests;

/// <summary>
/// The conventions a Pangea screen is built from, one test each. Copy this file for your own
/// screens: the shape does not change, only the assertions do.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PangeaTestServices"/> is the container a <c>ViewModelBase</c> asks for its services,
/// with doubles in it. Nothing here starts Avalonia.
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
        HomeViewModel screen = new(new PangeaTestServices());

        List<string?> raised = [];
        ((INotifyPropertyChanged)screen).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        screen.NewCustomer = "Ada Lovelace";

        Assert.Contains(nameof(HomeViewModel.NewCustomer), raised);
    }

    [Fact]
    public void ACommand_ReEvaluatesWhenItsDependencyChanges()
    {
        HomeViewModel screen = new(new PangeaTestServices());

        // Read once and held, exactly as a binding does: the notification has to reach this
        // instance, not a fresh one built by the next read of the property.
        RelayCommand add = screen.AddOrderCommand;

        int raised = 0;
        add.CanExecuteChanged += (_, _) => raised++;

        Assert.False(add.CanExecute(null));

        screen.NewCustomer = "Ada Lovelace";

        Assert.True(raised >= 1, $"Setting NewCustomer notified the command {raised} time(s).");
        Assert.True(add.CanExecute(null));
    }

    /// <summary>
    /// The rules are declared on the field and run in the generated setter, so a screen refuses
    /// bad input with nothing written in the view.
    /// </summary>
    [Fact]
    public void ValidationRules_RunFromTheGeneratedSetter()
    {
        HomeViewModel screen = new(new PangeaTestServices());

        screen.NewCustomer = "A";

        Assert.True(screen.HasErrors);
        Assert.False(screen.AddOrderCommand.CanExecute(null));

        screen.NewCustomer = "Ada Lovelace";

        Assert.False(screen.HasErrors);
    }

    [Fact]
    public void AddingAnOrder_PutsItInTheListAndClearsTheBox()
    {
        HomeViewModel screen = new(new PangeaTestServices()) { NewCustomer = "Ada Lovelace" };

        screen.AddOrderCommand.Execute(null);

        Assert.Contains(screen.Orders, order => order.Customer == "Ada Lovelace");
        Assert.Equal("", screen.NewCustomer);
    }

    /// <summary>
    /// A navigation is recorded rather than performed, so the request a screen sends can be
    /// asserted on without a shell, a view or a window.
    /// </summary>
    [Fact]
    public void OpeningAnOrder_SendsTheTypedRequest()
    {
        PangeaTestServices services = new();
        HomeViewModel screen = new(services);

        screen.OpenOrderCommand.Execute(new OrderSummary("ORD-0001", "Ada Lovelace"));

        Assert.Equal(typeof(OrderDetailViewModel), services.Navigation.LastDestination);
        Assert.Equal("ORD-0001", services.Navigation.LastRequest<ShowOrderDetail>()?.Reference);
    }
}
