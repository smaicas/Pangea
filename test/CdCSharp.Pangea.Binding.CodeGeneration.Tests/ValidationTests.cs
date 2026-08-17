using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using System.Collections;
using System.ComponentModel;
using System.Reflection;

namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// Validation rules declared where the field is declared, and enforced through
/// <see cref="INotifyDataErrorInfo"/> so Avalonia shows them without being told to.
/// </summary>
/// <remarks>
/// The rules are copied onto the generated property and evaluated by
/// <c>System.ComponentModel.DataAnnotations</c>, not re-implemented in emitted code. That is what
/// makes an application's own attribute work with no support from the generator.
/// </remarks>
public class ValidationTests
{
    private const string SignUp = """
        using CdCSharp.Pangea.Binding.Attributes;
        using CdCSharp.Pangea.Core.Base;
        using System.ComponentModel.DataAnnotations;

        namespace Sample;

        public partial class SignUpViewModel : ViewModelBase
        {
            public SignUpViewModel(IServiceProvider sp) : base(sp) { }

            [Binding]
            [Required(ErrorMessage = "An email is required.")]
            [EmailAddress(ErrorMessage = "That is not an email.")]
            private string _email = "";

            [Binding]
            [Range(18, 120, ErrorMessage = "Between 18 and 120.")]
            private int _age = 30;

            [Binding] private string _nickname = "";
        }
        """;

    private sealed class Services : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IRelayCommandFactory) ? new RelayCommandFactory(null) : null;
    }

    private static object Build(string source, string typeName)
    {
        Assembly assembly = GeneratorTestHelper.RunAndLoad(source);
        return Activator.CreateInstance(assembly.GetType("Sample." + typeName)!, new Services())!;
    }

    private static void Set(object viewModel, string property, object? value) =>
        viewModel.GetType().GetProperty(property)!.SetValue(viewModel, value);

    private static string[] ErrorsFor(object viewModel, string property) =>
        ((IEnumerable)((INotifyDataErrorInfo)viewModel).GetErrors(property)).Cast<string>().ToArray();

    [Fact]
    public void AValueThatBreaksARule_IsReportedAgainstItsProperty()
    {
        object viewModel = Build(SignUp, "SignUpViewModel");

        Set(viewModel, "Email", "not-an-email");

        Assert.Contains("That is not an email.", ErrorsFor(viewModel, "Email"));
        Assert.True(((INotifyDataErrorInfo)viewModel).HasErrors);
    }

    [Fact]
    public void CorrectingTheValue_ClearsTheError()
    {
        object viewModel = Build(SignUp, "SignUpViewModel");

        Set(viewModel, "Email", "not-an-email");
        Set(viewModel, "Email", "ada@example.com");

        Assert.Empty(ErrorsFor(viewModel, "Email"));
        Assert.False(((INotifyDataErrorInfo)viewModel).HasErrors);
    }

    [Fact]
    public void EachRuleOnAPropertyIsReported()
    {
        object viewModel = Build(SignUp, "SignUpViewModel");

        Set(viewModel, "Age", 5);

        Assert.Contains("Between 18 and 120.", ErrorsFor(viewModel, "Age"));
    }

    /// <summary>Avalonia listens to this to decorate the control that is wrong.</summary>
    [Fact]
    public void ErrorsChanged_NamesThePropertyThatChanged()
    {
        object viewModel = Build(SignUp, "SignUpViewModel");

        List<string?> changed = [];
        ((INotifyDataErrorInfo)viewModel).ErrorsChanged += (_, e) => changed.Add(e.PropertyName);

        Set(viewModel, "Email", "not-an-email");

        Assert.Equal(["Email"], changed);
    }

    [Fact]
    public void HasErrors_IsAnOrdinaryPropertyChange()
    {
        object viewModel = Build(SignUp, "SignUpViewModel");

        List<string?> raised = [];
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Set(viewModel, "Email", "not-an-email");

        Assert.Contains("HasErrors", raised);
    }

    /// <summary>A property with no rules is never wrong, and never disturbs anything.</summary>
    [Fact]
    public void APropertyWithoutRules_IsNotValidated()
    {
        object viewModel = Build(SignUp, "SignUpViewModel");

        List<string?> changed = [];
        ((INotifyDataErrorInfo)viewModel).ErrorsChanged += (_, e) => changed.Add(e.PropertyName);

        Set(viewModel, "Nickname", "ada");

        Assert.Empty(changed);
        Assert.False(((INotifyDataErrorInfo)viewModel).HasErrors);
    }

    /// <summary>
    /// Nothing is validated until it is touched, so a fresh form is not a wall of red. What a Save
    /// button asks for is the whole thing at once.
    /// </summary>
    [Fact]
    public void AFreshViewModelIsQuiet_UntilValidateAllIsAsked()
    {
        object viewModel = Build(SignUp, "SignUpViewModel");

        Assert.False(((INotifyDataErrorInfo)viewModel).HasErrors);

        bool valid = (bool)viewModel.GetType().GetMethod("ValidateAll")!.Invoke(viewModel, null)!;

        Assert.False(valid);
        Assert.Contains("An email is required.", ErrorsFor(viewModel, "Email"));
    }

    [Fact]
    public void ValidateAll_ReportsValidWhenEverythingIsFilledIn()
    {
        object viewModel = Build(SignUp, "SignUpViewModel");

        Set(viewModel, "Email", "ada@example.com");
        Set(viewModel, "Age", 36);

        Assert.True((bool)viewModel.GetType().GetMethod("ValidateAll")!.Invoke(viewModel, null)!);
    }

    /// <summary>
    /// The point of validating through DataAnnotations rather than emitting the checks: an
    /// application writes its own rule and the generator never hears about it.
    /// </summary>
    [Fact]
    public void AnApplicationsOwnValidationAttribute_Works()
    {
        object viewModel = Build("""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;
            using System.ComponentModel.DataAnnotations;

            namespace Sample;

            public sealed class NoShoutingAttribute : ValidationAttribute
            {
                public override bool IsValid(object? value) =>
                    value is not string text || text != text.ToUpperInvariant();
            }

            public partial class CommentViewModel : ViewModelBase
            {
                public CommentViewModel(IServiceProvider sp) : base(sp) { }

                [Binding]
                [NoShouting(ErrorMessage = "Please do not shout.")]
                private string _body = "";
            }
            """, "CommentViewModel");

        Set(viewModel, "Body", "HELLO");

        Assert.Contains("Please do not shout.", ErrorsFor(viewModel, "Body"));
    }

    /// <summary>
    /// A command guarded by HasErrors has to be re-evaluated when the errors move. HasErrors is
    /// declared on the base class, so this rides on the forwarding built for inherited dependencies.
    /// </summary>
    [Fact]
    public void ACommandGuardedByHasErrors_IsReEvaluatedWhenValidationChanges()
    {
        object viewModel = Build("""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;
            using System.ComponentModel.DataAnnotations;

            namespace Sample;

            public partial class FormViewModel : ViewModelBase
            {
                public FormViewModel(IServiceProvider sp) : base(sp) { }

                [Binding]
                [EmailAddress] private string _email = "ada@example.com";

                public RelayCommand SaveCommand => CreateCommand(() => { }, () => !HasErrors);
            }
            """, "FormViewModel");

        RelayCommand save = (RelayCommand)viewModel.GetType().GetProperty("SaveCommand")!.GetValue(viewModel)!;

        int raised = 0;
        save.CanExecuteChanged += (_, _) => raised++;

        Assert.True(save.CanExecute(null));

        Set(viewModel, "Email", "nope");

        Assert.True(raised > 0, "The command was never told its guard might have changed.");
        Assert.False(save.CanExecute(null));
    }
}
