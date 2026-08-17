using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Core.Base;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace CdCSharp.Pangea.Tests.Int.ViewModels;

/// <summary>
/// A form whose rules are declared on the fields and enforced by nothing visible in the view.
/// </summary>
/// <remarks>
/// The XAML binds Text and nothing else: no validation triggers, no error templates, no converters.
/// Everything on screen comes from the rules below.
/// </remarks>
public partial class ValidationDemoViewModel : ViewModelBase
{
    [Binding]
    [Required(ErrorMessage = "An email is required.")]
    [EmailAddress(ErrorMessage = "That does not look like an email address.")]
    private string _email = "";

    [Binding]
    [Range(18, 120, ErrorMessage = "Age has to be between 18 and 120.")]
    private int _age = 30;

    [Binding]
    [Required(ErrorMessage = "A display name is required.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "Between 3 and 20 characters.")]
    [NoShouting(ErrorMessage = "Please do not shout.")]
    private string _displayName = "";

    /// <summary>No rules at all, to show that it is never disturbed.</summary>
    [Binding] private string _notes = "";

    [Binding(ReadOnly = true)] private string _lastAction = "Nothing yet.";

    public ValidationDemoViewModel(IServiceProvider serviceProvider) : base(serviceProvider) =>
        ErrorsChanged += (_, _) => OnPropertyChanged(nameof(ErrorSummary));

    /// <summary>Every message currently outstanding, from <c>GetErrors(null)</c>.</summary>
    public string ErrorSummary
    {
        get
        {
            string[] messages = GetErrors(null).Cast<object>()
                .Select(message => "• " + message)
                .ToArray();

            return messages.Length == 0 ? "No errors." : string.Join(Environment.NewLine, messages);
        }
    }

    public bool CanSave => !HasErrors;

    public RelayCommand SaveCommand => CreateCommand(Save, () => CanSave);

    public RelayCommand CheckEverythingCommand => CreateCommand(CheckEverything);

    private void Save()
    {
        _lastAction = $"Saved at {DateTime.Now:HH:mm:ss}.";
        OnPropertyChanged(nameof(LastAction));
    }

    /// <summary>
    /// What a Save button asks before doing anything: an untouched field has never been validated,
    /// so a form nobody filled in looks valid until it is asked.
    /// </summary>
    private void CheckEverything()
    {
        bool valid = ValidateAll();

        _lastAction = valid
            ? $"ValidateAll said the form is valid ({DateTime.Now:HH:mm:ss})."
            : $"ValidateAll found problems ({DateTime.Now:HH:mm:ss}).";

        OnPropertyChanged(nameof(LastAction));
    }
}

/// <summary>An application's own rule, which the generator knows nothing about.</summary>
public sealed class NoShoutingAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string text || text.Length < 3) return true;

        // Digits and punctuation are unchanged by ToUpper, so "123" reads as shouting unless the
        // rule is about letters. Nothing to shout with, nothing to complain about.
        if (!text.Any(char.IsLetter)) return true;

        return text != text.ToUpperInvariant();
    }
}
