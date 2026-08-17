using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CdCSharp.Pangea.Theming.Tests.Infrastructure;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace CdCSharp.Pangea.Theming.Tests;

/// <summary>
/// A bound control showing a validation error, under the toolkit theme.
/// </summary>
/// <remarks>
/// The claim in the documentation - declare a rule and the control decorates itself - crosses four
/// layers: the property raising <see cref="INotifyDataErrorInfo"/>, Avalonia's binding turning that
/// into <c>DataValidationErrors</c>, the control template consuming it, and the vendored theme
/// having kept that template. Nothing tested the whole run, so it was an assumption in shipped
/// documentation rather than a fact.
/// </remarks>
public class ValidationRenderingTests
{
    /// <summary>
    /// Written the way the generator writes it: a rule on the property, and a validation call in
    /// the setter.
    /// </summary>
    private sealed class Subject : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private readonly Dictionary<string, string[]> _errors = [];
        private string _email = "";

        [Required(ErrorMessage = "An email is required.")]
        [EmailAddress(ErrorMessage = "That is not an email.")]
        public string Email
        {
            get => _email;
            set
            {
                if (_email == value) return;

                _email = value;
                Validate(value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Email)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public bool HasErrors => _errors.Count > 0;

        public IEnumerable GetErrors(string? propertyName) =>
            propertyName is not null && _errors.TryGetValue(propertyName, out string[]? found)
                ? found
                : Array.Empty<string>();

        private void Validate(object? value, [CallerMemberName] string propertyName = "")
        {
            List<ValidationResult> results = [];

            Validator.TryValidateProperty(
                value, new ValidationContext(this) { MemberName = propertyName }, results);

            if (results.Count == 0) _errors.Remove(propertyName);
            else _errors[propertyName] = [.. results.Select(r => r.ErrorMessage ?? "Invalid.")];

            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
    }

    private static (Window Window, TextBox Box) Bind(Subject subject)
    {
        _ = ThemeHarness.Service;   // the toolkit theme, applied as an application applies it

        TextBox box = new() { DataContext = subject };

        box.Bind(TextBox.TextProperty, new Binding(nameof(Subject.Email)) { Mode = BindingMode.TwoWay });

        Window window = new() { Content = box };
        window.Show();

        return (window, box);
    }

    [AvaloniaFact]
    public void AValidValue_LeavesTheControlUndecorated()
    {
        Subject subject = new();
        (Window window, TextBox box) = Bind(subject);

        box.Text = "ada@example.com";

        Assert.False(DataValidationErrors.GetHasErrors(box));
        window.Close();
    }

    /// <summary>The whole point: nothing in the view asked for this.</summary>
    [AvaloniaFact]
    public void AnInvalidValue_MarksTheControlAsHavingErrors()
    {
        Subject subject = new();
        (Window window, TextBox box) = Bind(subject);

        box.Text = "not-an-email";

        Assert.True(DataValidationErrors.GetHasErrors(box));
        window.Close();
    }

    [AvaloniaFact]
    public void TheMessageTheRuleDeclared_ReachesTheControl()
    {
        Subject subject = new();
        (Window window, TextBox box) = Bind(subject);

        box.Text = "not-an-email";

        IEnumerable<object>? errors = DataValidationErrors.GetErrors(box);

        Assert.NotNull(errors);
        Assert.Contains(errors!, error => error?.ToString() == "That is not an email.");
        window.Close();
    }

    [AvaloniaFact]
    public void CorrectingTheValue_ClearsTheDecoration()
    {
        Subject subject = new();
        (Window window, TextBox box) = Bind(subject);

        box.Text = "not-an-email";
        Assert.True(DataValidationErrors.GetHasErrors(box));

        box.Text = "ada@example.com";

        Assert.False(DataValidationErrors.GetHasErrors(box));
        window.Close();
    }

    /// <summary>
    /// The theme has to still carry the piece that draws it. It is vendored from Avalonia, and a
    /// dictionary dropped during an upgrade would take the error message off the screen while
    /// everything else kept working.
    /// </summary>
    [AvaloniaFact]
    public void TheThemeStillTemplatesTheErrorPresenter()
    {
        Subject subject = new();
        (Window window, TextBox box) = Bind(subject);

        box.Text = "not-an-email";
        box.ApplyTemplate();

        // The visual tree, not the logical one: template children live there.
        DataValidationErrors? presenter = box.GetVisualDescendants()
            .OfType<DataValidationErrors>()
            .FirstOrDefault();

        Assert.True(presenter is not null,
            "The TextBox template has no DataValidationErrors, so nothing draws the message.");

        window.Close();
    }
}
