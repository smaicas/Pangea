using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Localization;
using Microsoft.Extensions.DependencyInjection;
using PangeaShellApp.Navigation;

namespace PangeaShellApp.ViewModels;

/// <summary>
/// Receives the request typed. Nothing here is cast, and nothing here reads a dictionary of
/// parameters.
/// </summary>
/// <remarks>
/// <c>OnNavigatedToAsync(ShowOrderDetail)</c> is the arrival hook for that request; the
/// parameterless one inherited from <see cref="ViewModelBase"/> runs instead when the screen is
/// reached without one - going back, for instance.
/// </remarks>
public partial class OrderDetailViewModel : ViewModelBase, INavigationAware<ShowOrderDetail>
{
    [Binding(ReadOnly = true)] private string _customer = "";
    [Binding(ReadOnly = true)] private string _reference = "";

    public OrderDetailViewModel(IServiceProvider serviceProvider) : base(serviceProvider) =>
        Strings = serviceProvider.GetRequiredService<LocalizedStrings>();

    public LocalizedStrings Strings { get; }

    public Task OnNavigatedToAsync(ShowOrderDetail request)
    {
        // Read-only properties have no setter to notify from, so the change is announced here.
        _customer = request.Customer;
        _reference = request.Reference;

        OnPropertyChanged(nameof(Customer));
        OnPropertyChanged(nameof(Reference));

        return Task.CompletedTask;
    }
}
