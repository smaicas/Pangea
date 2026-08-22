using CdCSharp.Pangea.Core.Abstractions;
using PangeaShellApp.ViewModels;

namespace PangeaShellApp.Navigation;

/// <summary>
/// Carries the order to open, and names the screen that opens it.
/// </summary>
/// <remarks>
/// The destination is part of the type, so <c>NavigateToAsync(new ShowOrderDetail(...))</c> needs
/// no type argument and cannot be sent somewhere that would ignore it: a request whose destination
/// does not implement <c>INavigationAware&lt;ShowOrderDetail&gt;</c> fails at startup, not on the
/// navigation that drops the data.
/// </remarks>
public sealed record ShowOrderDetail(string Reference, string Customer)
    : INavigationRequest<OrderDetailViewModel>;
