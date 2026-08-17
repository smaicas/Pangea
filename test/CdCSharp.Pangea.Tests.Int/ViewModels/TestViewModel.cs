using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace CdCSharp.Pangea.Tests.ViewModels;

/// <summary>
/// ViewModel de prueba que ejercita todas las capacidades del analizador funcional
/// </summary>
public partial class TestViewModel : ViewModelBase
{
    #region Basic Binding Fields

    [Binding] private string _name = string.Empty;
    [Binding] private int _age;
    [Binding] private bool _isEnabled;
    [Binding] private double _salary;
    [Binding(ReadOnly = true)] private string _id = Guid.NewGuid().ToString();

    #endregion

    #region Complex Binding Fields

    [Binding] private string _firstName = string.Empty;
    [Binding] private string _lastName = string.Empty;
    [Binding] private string _email = string.Empty;
    [Binding] private string _password = string.Empty;
    [Binding] private string _confirmPassword = string.Empty;
    [Binding] private bool _isLoading;
    [Binding] private bool _hasErrors;
    [Binding] private int _itemCount;
    [Binding] private bool _isOnline;
    [Binding] private bool _isAuthenticated;

    #endregion

    #region Collections

    [Binding] private ObservableCollection<string> _items;
    [Binding] private ObservableCollection<TestItem> _testItems;
    [Binding] private bool _isRecording;

    #endregion

    #region Computed Properties (Expression Body)

    // Computed property simple
    public string FullName => $"{FirstName} {LastName}";

    // Computed property con lógica
    public bool IsAdult => Age >= 18;

    // Computed property compleja
    public string DisplayText => IsEnabled ? $"{FullName} ({Age})" : "Disabled";

    // Computed property que depende de colección
    public bool HasItems => Items.Count > 0;

    // Computed property con múltiples dependencias
    public string Status => IsLoading ? "Loading..." : 
                           HasErrors ? "Error" : 
                           IsOnline ? "Online" : "Offline";

    #endregion

    #region Computed Properties (Complex Body)

    // Computed property con getter complejo
    public bool CanProceed
    {
        get
        {
            if (IsLoading) return false;
            if (!IsEnabled) return false;
            if (string.IsNullOrEmpty(FirstName) || string.IsNullOrEmpty(LastName)) return false;
            return Age >= 18;
        }
    }

    // Computed property con validación
    public bool IsPasswordValid
    {
        get
        {
            if (string.IsNullOrEmpty(Password)) return false;
            if (Password.Length < 8) return false;
            if (Password != ConfirmPassword) return false;
            return true;
        }
    }

    // Computed property con lógica de negocio
    public decimal NetSalary
    {
        get
        {
            if (Salary <= 0) return 0;
            decimal tax = (decimal)(Salary * 0.2);
            return (decimal)Salary - tax;
        }
    }

    #endregion

    #region CanExecute Methods

    // CanExecute simple
    public bool CanSave => !IsLoading && !HasErrors;

    // CanExecute con múltiples condiciones
    public bool CanSubmit => !IsLoading && IsPasswordValid && CanProceed;

    // CanExecute que depende de computed properties
    public bool CanDelete => HasItems && IsAuthenticated && !IsLoading;

    // CanExecute con OR logic (caso crítico del AutomationViewModel)
    public bool CanRecord => !IsLoading && IsOnline && !IsRecording;
    public bool CanStopRecording => IsRecording;

    // CanExecute con lógica compleja (método)
    public bool CanExecuteComplexOperation()
    {
        if (!IsOnline) return false;
        if (!IsAuthenticated) return false;
        if (IsLoading) return false;
        if (ItemCount < 5) return false;
        return Age >= 21;
    }

    // CanExecute con validación de email
    public bool CanSendEmail()
    {
        if (string.IsNullOrEmpty(Email)) return false;
        if (!Email.Contains("@")) return false;
        return IsOnline && !IsLoading;
    }

    #endregion

    #region Commands

    public RelayCommand SaveCommand { get; }
    public RelayCommand SubmitCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand ComplexOperationCommand { get; }
    public RelayCommand SendEmailCommand { get; }
    public RelayCommand<string> AddItemCommand { get; }
    public RelayCommand<int> RemoveItemCommand { get; }
    public RelayCommand ClearCommand { get; }
    
    // Comando con lambda compleja (caso del AutomationViewModel)
    public RelayCommand ToggleRecordingCommand { get; }

    #endregion

    #region Constructor

    public TestViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _items = new ObservableCollection<string>();
        _testItems = new ObservableCollection<TestItem>();

        // Comandos con lambdas simples (casos más comunes)
        SaveCommand = CreateCommand(SaveAsync, () => CanSave);
        SubmitCommand = CreateCommand(SubmitAsync, () => CanSubmit);
        DeleteCommand = CreateCommand(DeleteAsync, () => CanDelete);

        // Comandos con referencias a métodos
        ComplexOperationCommand = CreateCommand(ExecuteComplexOperationAsync, CanExecuteComplexOperation);
        SendEmailCommand = CreateCommand(SendEmailAsync, CanSendEmail);

        // Comandos con parámetros
        AddItemCommand = CreateCommand<string>(AddItem, item => !string.IsNullOrEmpty(item) && !IsLoading);
        RemoveItemCommand = CreateCommand<int>(RemoveItem, index => index >= 0 && index < Items.Count && !IsLoading);

        // Comando sin CanExecute
        RefreshCommand = CreateCommand(RefreshAsync);
        ClearCommand = CreateCommand(ClearItems);
        
        // Caso crítico: Comando con lambda compleja OR (como ToggleRecordingCommand del AutomationViewModel)
        ToggleRecordingCommand = CreateCommand(ToggleRecordingAsync, () => CanRecord || CanStopRecording);

        // Inicializar algunos datos
        Age = 25;
        FirstName = "John";
        LastName = "Doe";
        Email = "john.doe@example.com";
        IsOnline = true;
        IsAuthenticated = true;
    }

    #endregion

    #region Command Implementations

    private async Task SaveAsync()
    {
        IsLoading = true;
        HasErrors = false;

        try
        {
            await SimulateNetworkCall();
            // Simulate save logic
        }
        catch
        {
            HasErrors = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SubmitAsync()
    {
        IsLoading = true;
        HasErrors = false;

        try
        {
            await SimulateNetworkCall();
            // Simulate submit logic
            OnSubmissionCompleted();
        }
        catch
        {
            HasErrors = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (HasItems)
        {
            IsLoading = true;
            try
            {
                await SimulateNetworkCall();
                ClearItems();
                UpdateItemCount();
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    private async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            await SimulateNetworkCall();
            // Simulate refresh logic
            LoadTestData();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExecuteComplexOperationAsync()
    {
        IsLoading = true;
        try
        {
            await SimulateComplexOperation();
            UpdateItemCount();
            NotifyComplexOperationCompleted();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SendEmailAsync()
    {
        IsLoading = true;
        try
        {
            await SimulateEmailSending();
            // Email sent successfully
        }
        catch
        {
            HasErrors = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void AddItem(string? item)
    {
        if (!string.IsNullOrEmpty(item))
        {
            Items.Add(item);
            UpdateItemCount();
            NotifyItemsChanged();
        }
    }

    private void RemoveItem(int index)
    {
        if (index >= 0 && index < Items.Count)
        {
            Items.RemoveAt(index);
            UpdateItemCount();
            NotifyItemsChanged();
        }
    }

    private void ClearItems()
    {
        Items.Clear();
        TestItems.Clear();
        UpdateItemCount();
        NotifyItemsChanged();
    }

    private async Task ToggleRecordingAsync()
    {
        IsRecording = !IsRecording;
        
        if (IsRecording)
        {
            IsLoading = true;
            await SimulateNetworkCall();
            IsLoading = false;
        }
        else
        {
            await SimulateNetworkCall();
        }
    }

    #endregion

    #region Partial Methods (para testing del analizador)

    partial void OnNameChanged()
    {
        ValidateName();
        UpdateDisplayText();
    }

    partial void OnFirstNameChanged()
    {
        ValidateName();
        UpdateFullName();
        CheckCanProceed();
    }

    partial void OnLastNameChanged()
    {
        ValidateName();
        UpdateFullName();
        CheckCanProceed();
    }

    partial void OnAgeChanged()
    {
        ValidateAge();
        CheckAdultStatus();
        UpdateSalaryCalculations();
    }

    partial void OnIsEnabledChanged()
    {
        UpdateDisplayText();
        CheckCanProceed();
        NotifyStateChanged();
    }

    partial void OnPasswordChanged()
    {
        ValidatePassword();
        CheckPasswordMatch();
    }

    partial void OnConfirmPasswordChanged()
    {
        CheckPasswordMatch();
    }

    partial void OnIsLoadingChanged()
    {
        NotifyLoadingStateChanged();
        UpdateAllCanExecute();
    }

    partial void OnHasErrorsChanged()
    {
        NotifyErrorStateChanged();
        UpdateAllCanExecute();
    }

    partial void OnIsOnlineChanged()
    {
        NotifyConnectionStateChanged();
        UpdateNetworkDependentCommands();
    }

    partial void OnIsAuthenticatedChanged()
    {
        NotifyAuthenticationStateChanged();
        UpdateSecureCommands();
    }

    #endregion

    #region Collection Modifying Methods (para testing)

    private void LoadTestData()
    {
        Items.Add("Test Item 1");
        Items.Add("Test Item 2"); 
        Items.Add("Test Item 3");
        
        TestItems.Add(new TestItem { Name = "Item A", Value = 100 });
        TestItems.Add(new TestItem { Name = "Item B", Value = 200 });
        
        UpdateItemCount();
        // Manual notifications for testing
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(DisplayText));
    }

    private void UpdateItemCount()
    {
        ItemCount = Items.Count + TestItems.Count;
    }

    private void NotifyItemsChanged()
    {
        // Método que modifica colecciones y notifica manualmente
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(Status));
    }

    #endregion

    #region Helper Methods

    private void ValidateName()
    {
        HasErrors = string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(FirstName) || string.IsNullOrEmpty(LastName);
    }

    private void ValidateAge()
    {
        HasErrors = Age < 0 || Age > 120;
    }

    private void ValidatePassword()
    {
        HasErrors = string.IsNullOrEmpty(Password) || Password.Length < 8;
    }

    private void CheckPasswordMatch()
    {
        HasErrors = Password != ConfirmPassword;
    }

    private void UpdateDisplayText()
    {
        // Trigger recalculation of DisplayText computed property
        OnPropertyChanged(nameof(DisplayText));
    }

    private void UpdateFullName()
    {
        OnPropertyChanged(nameof(FullName));
    }

    private void CheckCanProceed()
    {
        OnPropertyChanged(nameof(CanProceed));
    }

    private void CheckAdultStatus()
    {
        OnPropertyChanged(nameof(IsAdult));
    }

    private void UpdateSalaryCalculations()
    {
        OnPropertyChanged(nameof(NetSalary));
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(Status));
    }

    private void NotifyLoadingStateChanged()
    {
        OnPropertyChanged(nameof(Status));
    }

    private void NotifyErrorStateChanged()
    {
        OnPropertyChanged(nameof(Status));
    }

    private void NotifyConnectionStateChanged()
    {
        OnPropertyChanged(nameof(Status));
    }

    private void NotifyAuthenticationStateChanged()
    {
        // Authentication state changed - update secure operations
    }

    private void UpdateAllCanExecute()
    {
        // Method that should trigger command updates
    }

    private void UpdateNetworkDependentCommands()
    {
        // Network state changed - update network dependent commands
    }

    private void UpdateSecureCommands()
    {
        // Authentication changed - update secure commands  
    }

    private void OnSubmissionCompleted()
    {
        // Handle submission completion
        IsAuthenticated = true;
        UpdateItemCount();
    }

    private void NotifyComplexOperationCompleted()
    {
        // Complex operation completed
        OnPropertyChanged(nameof(Status));
    }

    #endregion

    #region Simulation Methods

    private static async Task SimulateNetworkCall()
    {
        await Task.Delay(1000);
    }

    private static async Task SimulateComplexOperation()
    {
        await Task.Delay(2000);
    }

    private static async Task SimulateEmailSending()
    {
        await Task.Delay(1500);
    }

    #endregion
}

#region Supporting Classes

public class TestItem
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

#endregion