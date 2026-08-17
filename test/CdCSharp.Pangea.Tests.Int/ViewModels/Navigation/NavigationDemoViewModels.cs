using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Navigation.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace CdCSharp.Pangea.Tests.Int.ViewModels.Navigation;

/// <summary>Carries the order to open, and names the screen that opens it.</summary>
public sealed record ShowOrderDetail(Guid Id, string Customer) : INavigationRequest<OrderDetailViewModel>;

public partial class HomeViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    [Binding] private int _visits;

    public HomeViewModel(IServiceProvider serviceProvider) : base(serviceProvider) =>
        _navigation = serviceProvider.GetRequiredService<INavigationService>();

    public string Summary => $"Home has been shown {Visits} time(s) in this session.";

    public RelayCommand OpenFirstOrderCommand =>
        CreateCommand(() => _navigation.NavigateToAsync(new ShowOrderDetail(Guid.NewGuid(), "Ada Lovelace")));

    public override Task OnNavigatedToAsync()
    {
        Visits++;
        return Task.CompletedTask;
    }

    partial void OnVisitsChanged() => OnPropertyChanged(nameof(Summary));
}

/// <summary>Receives the request typed; nothing is cast.</summary>
public partial class OrderDetailViewModel : ViewModelBase, INavigationAware<ShowOrderDetail>
{
    [Binding(ReadOnly = true)] private string _customer = "(nothing yet)";
    [Binding(ReadOnly = true)] private string _orderId = "(nothing yet)";
    [Binding] private int _arrivals;

    public OrderDetailViewModel(IServiceProvider serviceProvider) : base(serviceProvider) { }

    public Task OnNavigatedToAsync(ShowOrderDetail request)
    {
        _customer = request.Customer;
        _orderId = request.Id.ToString();
        _arrivals++;

        OnPropertyChanged(nameof(Customer));
        OnPropertyChanged(nameof(OrderId));
        OnPropertyChanged(nameof(Arrivals));
        return Task.CompletedTask;
    }
}

/// <summary>Refuses to leave while it says it has unsaved work.</summary>
public partial class SettingsViewModel : ViewModelBase
{
    [Binding] private bool _hasUnsavedChanges;
    [Binding(ReadOnly = true)] private string _lastRefusal = "";

    public SettingsViewModel(IServiceProvider serviceProvider) : base(serviceProvider) { }

    public override Task<bool> CanNavigateAwayAsync()
    {
        if (!HasUnsavedChanges) return Task.FromResult(true);

        _lastRefusal = $"Refused to leave at {DateTime.Now:HH:mm:ss} - untick the box to allow it.";
        OnPropertyChanged(nameof(LastRefusal));
        return Task.FromResult(false);
    }
}
