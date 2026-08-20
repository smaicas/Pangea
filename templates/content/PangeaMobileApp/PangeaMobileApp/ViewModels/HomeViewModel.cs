using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace PangeaMobileApp.ViewModels;

/// <summary>
/// The first screen, and an example of the three conventions worth copying.
/// </summary>
/// <remarks>
/// A <c>[Binding]</c> field becomes an observable property; a computed property that reads it is
/// notified when it changes; and a command whose <c>CanExecute</c> reads that property is
/// re-evaluated from its setter. None of that is written by hand.
/// </remarks>
public partial class HomeViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;

    [Binding] private string _name = "";
    [Binding] private int _taps;

    public HomeViewModel(IServiceProvider serviceProvider) : base(serviceProvider) =>
        _dialogs = serviceProvider.GetRequiredService<IDialogService>();

    /// <summary>Reads Name, so the generator raises CanExecuteChanged from its setter.</summary>
    public bool CanGreet => !string.IsNullOrWhiteSpace(Name);

    public string Greeting => Taps == 0 ? "" : $"Hello {Name} - {Taps} taps";

    public RelayCommand GreetCommand => CreateCommand(Greet, () => CanGreet);

    public RelayCommand AboutCommand => CreateCommand(AboutAsync);

    private void Greet() => Taps++;

    /// <summary>
    /// A dialog, without a window being written for it.
    /// </summary>
    /// <remarks>
    /// On desktop this is a modal window; on a phone it is a card layered over the shell, because
    /// there is no window to open. The call site does not know or care which.
    /// </remarks>
    private Task AboutAsync() =>
        _dialogs.AlertAsync("PangeaMobileApp", "An Avalonia application built on Pangea.");

    /// <summary>Called by the generated setter, after the change, before notifications.</summary>
    partial void OnTapsChanged() => OnPropertyChanged(nameof(Greeting));
}
