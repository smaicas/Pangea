using static CdCSharp.Pangea.Binding.CodeGeneration.Tests.ComputedPropertyNotificationTests;

namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// Commands must re-evaluate their CanExecute whenever a bound property they depend on changes.
/// </summary>
public class CommandNotificationTests
{
    [Fact]
    public void Command_WithLambdaCanExecute_IsRaisedByTheReferencedProperty()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class EditorViewModel : ViewModelBase
            {
                public EditorViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private bool _isDirty;

                public RelayCommand SaveCommand => CreateCommand(Save, () => IsDirty);

                private void Save() { }
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "EditorViewModel");

        Assert.Contains("SaveCommand.RaiseCanExecuteChanged();", generated);
    }

    [Fact]
    public void Command_WithCanExecuteMethodReference_FollowsTheMethodDependencies()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class EditorViewModel : ViewModelBase
            {
                public EditorViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _title = string.Empty;
                [Binding] private bool _isBusy;

                public RelayCommand SaveCommand => CreateCommand(Save, CanSave);

                private bool CanSave() => Title.Length > 0 && !IsBusy;

                private void Save() { }
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "EditorViewModel");

        Assert.Equal(2, CountOccurrences(generated, "SaveCommand.RaiseCanExecuteChanged();"));
    }

    [Fact]
    public void Command_WithCanExecuteProperty_FollowsThatProperty()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class EditorViewModel : ViewModelBase
            {
                public EditorViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _title = string.Empty;

                public bool CanSave => Title.Length > 0;

                public RelayCommand SaveCommand => CreateCommand(Save, () => CanSave);

                private void Save() { }
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "EditorViewModel");
        string titleSetter = ExtractPropertyBody(generated, "public string Title");

        Assert.Contains("SaveCommand.RaiseCanExecuteChanged();", titleSetter);
        // The CanExecute property is also a computed property, so it must be refreshed too.
        Assert.Contains("OnPropertyChanged(nameof(CanSave));", titleSetter);
    }

    [Fact]
    public void Command_WithBinaryCanExecute_IsRaisedByEveryOperand()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class EditorViewModel : ViewModelBase
            {
                public EditorViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private bool _isDirty;
                [Binding] private bool _isBusy;
                [Binding] private int _unrelated;

                public RelayCommand SaveCommand => CreateCommand(Save, () => IsDirty && !IsBusy);

                private void Save() { }
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "EditorViewModel");

        Assert.Contains("SaveCommand.RaiseCanExecuteChanged();",
            ExtractPropertyBody(generated, "public bool IsDirty"));
        Assert.Contains("SaveCommand.RaiseCanExecuteChanged();",
            ExtractPropertyBody(generated, "public bool IsBusy"));
        Assert.DoesNotContain("SaveCommand.RaiseCanExecuteChanged();",
            ExtractPropertyBody(generated, "public int Unrelated"));
    }

    [Fact]
    public void Command_AssignedInConstructor_IsDetected()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class EditorViewModel : ViewModelBase
            {
                [Binding] private bool _isDirty;

                public RelayCommand SaveCommand { get; private set; }

                public EditorViewModel(IServiceProvider sp) : base(sp)
                {
                    SaveCommand = CreateCommand(Save, () => IsDirty);
                }

                private void Save() { }
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "EditorViewModel");

        Assert.Contains("SaveCommand.RaiseCanExecuteChanged();", generated);
    }

    [Fact]
    public void Command_WithoutCanExecute_IsNeverRaised()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class EditorViewModel : ViewModelBase
            {
                public EditorViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private bool _isDirty;

                public RelayCommand SaveCommand => CreateCommand(Save);

                private void Save() { }
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "EditorViewModel");

        Assert.DoesNotContain("RaiseCanExecuteChanged", generated);
    }

    [Fact]
    public void Command_DependingOnComputedProperty_IsRaisedByTheUnderlyingBinding()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class EditorViewModel : ViewModelBase
            {
                public EditorViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _email = string.Empty;

                public bool IsEmailValid => Email.Contains("@");
                public bool CanSubmit => IsEmailValid;

                public RelayCommand SubmitCommand => CreateCommand(Submit, () => CanSubmit);

                private void Submit() { }
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "EditorViewModel");
        string emailSetter = ExtractPropertyBody(generated, "public string Email");

        Assert.Contains("SubmitCommand.RaiseCanExecuteChanged();", emailSetter);
    }
}
