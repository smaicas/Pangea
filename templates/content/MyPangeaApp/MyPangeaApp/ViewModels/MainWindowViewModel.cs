using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;

namespace MyPangeaApp.ViewModels;

/// <summary>
/// Shows the three conventions worth copying: [Binding] fields, a computed property that the
/// generator wires notifications for, and a command gated on that property.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(IServiceProvider services) : base(services) { }

    [Binding] private string _name = "world";
    [Binding] private int _clicks;

    // Reads the generated Name property, so it is notified whenever Name changes.
    public string Greeting => string.IsNullOrWhiteSpace(Name) ? "Hello!" : $"Hello, {Name}!";

    public bool CanGreet => !string.IsNullOrWhiteSpace(Name);

    // Synchronous body: runs on the UI thread. CanGreet reads Name, so the generator raises
    // CanExecuteChanged from Name's setter.
    public RelayCommand GreetCommand => CreateCommand(Greet, () => CanGreet);

    private void Greet() => Clicks++;
}
