using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Localization;
using CdCSharp.Pangea.Navigation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using PangeaShellApp.Navigation;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace PangeaShellApp.ViewModels;

/// <summary>One row of the list. A record because nothing edits it in place.</summary>
public sealed record OrderSummary(string Reference, string Customer);

/// <summary>
/// The list, plus the form that adds to it.
/// </summary>
/// <remarks>
/// Shows the three conventions worth copying: <c>[Binding]</c> fields becoming observable
/// properties, validation rules declared on the field and enforced with no help from the view, and
/// a command gated on a property the generator knows the rule reads.
/// </remarks>
public partial class HomeViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    private int _nextReference = 1;

    [Binding]
    [Required(ErrorMessage = "A customer name is required.")]
    [StringLength(40, MinimumLength = 2, ErrorMessage = "Between 2 and 40 characters.")]
    private string _newCustomer = "";

    [Binding] private OrderSummary? _selectedOrder;

    public HomeViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _navigation = serviceProvider.GetRequiredService<INavigationService>();
        Strings = serviceProvider.GetRequiredService<LocalizedStrings>();

        // The collection notifies about itself; whether it is empty is a separate property, and
        // nothing would tell the view that it changed.
        Orders.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasOrders));
    }

    public LocalizedStrings Strings { get; }

    public ObservableCollection<OrderSummary> Orders { get; } =
    [
        new("ORD-0001", "Ada Lovelace"),
        new("ORD-0002", "Grace Hopper")
    ];

    public bool HasOrders => Orders.Count > 0;

    /// <summary>Reads NewCustomer, so the generator raises CanExecuteChanged from its setter.</summary>
    public bool CanAddOrder => !string.IsNullOrWhiteSpace(NewCustomer) && !HasErrors;

    public RelayCommand AddOrderCommand => CreateCommand(AddOrder, () => CanAddOrder);

    /// <summary>
    /// Takes the order as its parameter rather than reading the selection, so the same command
    /// works from a button, a menu, or anywhere else that can name one.
    /// </summary>
    public RelayCommand<OrderSummary> OpenOrderCommand =>
        CreateCommand<OrderSummary>(Open, order => order is not null);

    private void AddOrder()
    {
        // The rules run on every keystroke through the generated setter; this is the check the
        // button makes before acting on what they said.
        if (!ValidateAll()) return;

        _nextReference = Orders.Count + 1;
        Orders.Add(new OrderSummary($"ORD-{_nextReference:0000}", NewCustomer.Trim()));

        NewCustomer = "";
    }

    private void Open(OrderSummary? order)
    {
        if (order is null) return;

        _ = _navigation.NavigateToAsync(new ShowOrderDetail(order.Reference, order.Customer));
    }
}
