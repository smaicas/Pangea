using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;
using System;
using System.Threading.Tasks;

namespace CdCSharp.Pangea.Tests.Int.ViewModels;

public partial class CommandTestViewModel : ViewModelBase
{
    [Binding] private string _textInput = string.Empty;
    [Binding] private bool _isEnabled = true;
    [Binding] private bool _isLoading = false;
    [Binding] private int _counter = 0;
    [Binding] private string _statusMessage = "Ready to test commands";

    public CommandTestViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        // Crear comandos en constructor - Una sola vez
        BasicCommand = CreateCommand(ExecuteBasicCommand, () => CanExecuteBasicCommand);
        TextCommand = CreateCommand(ExecuteTextCommand, () => CanExecuteTextCommand);
        LoadingCommand = CreateCommand(ExecuteLoadingCommand, () => !IsLoading);
        ResetCommand = CreateCommand(ExecuteReset, () => CanReset);
        ToggleEnabledCommand = CreateCommand(ExecuteToggleEnabled);
    }

    // Computed properties para CanExecute
    public bool CanExecuteBasicCommand 
    { 
        get 
        {
            bool result = IsEnabled && !IsLoading;
            System.Diagnostics.Debug.WriteLine($"[ViewModel] CanExecuteBasicCommand evaluado: {result} (IsEnabled={IsEnabled}, IsLoading={IsLoading})");
            return result;
        }
    }
    
    public bool CanExecuteTextCommand 
    { 
        get 
        {
            bool result = !string.IsNullOrWhiteSpace(TextInput) && !IsLoading;
            System.Diagnostics.Debug.WriteLine($"[ViewModel] CanExecuteTextCommand evaluado: {result} (TextInput='{TextInput}', IsLoading={IsLoading})");
            return result;
        }
    }
    
    public bool CanReset => Counter > 0 || !string.IsNullOrEmpty(TextInput);

    // Comandos como propiedades auto-implementadas
    public RelayCommand BasicCommand { get; }
    public RelayCommand TextCommand { get; }
    public RelayCommand LoadingCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand ToggleEnabledCommand { get; }

    private void ExecuteBasicCommand()
    {
        Counter++;
        StatusMessage = $"Basic command executed. Counter: {Counter}";
    }

    private void ExecuteTextCommand()
    {
        StatusMessage = $"Text command executed with: '{TextInput}'";
        Counter++;
    }

    private async Task ExecuteLoadingCommand()
    {
        IsLoading = true;
        StatusMessage = "Loading command executing...";
        
        try
        {
            await Task.Delay(2000); // Simular operación larga
            StatusMessage = "Loading command completed!";
            Counter++;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ExecuteReset()
    {
        Counter = 0;
        TextInput = string.Empty;
        StatusMessage = "Reset completed";
    }

    private void ExecuteToggleEnabled()
    {
        IsEnabled = !IsEnabled;
        StatusMessage = $"Enabled state changed to: {IsEnabled}";
    }

    // Debug partial methods
    partial void OnTextInputChanged()
    {
        System.Diagnostics.Debug.WriteLine($"[ViewModel] TextInput cambió a: '{TextInput}'");
        System.Diagnostics.Debug.WriteLine($"[ViewModel] CanExecuteTextCommand es ahora: {CanExecuteTextCommand}");
    }

    partial void OnIsEnabledChanged()
    {
        System.Diagnostics.Debug.WriteLine($"[ViewModel] IsEnabled cambió a: {IsEnabled}");
    }

    partial void OnIsLoadingChanged()
    {
        System.Diagnostics.Debug.WriteLine($"[ViewModel] IsLoading cambió a: {IsLoading}");
    }
}