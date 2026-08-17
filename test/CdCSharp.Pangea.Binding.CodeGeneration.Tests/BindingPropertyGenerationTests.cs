namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// Covers the core contract of the generator: turning <c>[Binding]</c> fields into
/// observable properties on a partial ViewModel.
/// </summary>
public class BindingPropertyGenerationTests
{
    [Fact]
    public void BindingField_GeneratesPublicProperty_BackedByField()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class PersonViewModel : ViewModelBase
            {
                public PersonViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _name = string.Empty;
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "PersonViewModel");

        Assert.Contains("public string Name", generated);
        Assert.Contains("get => _name;", generated);
        Assert.Contains("if (SetProperty(ref _name, value))", generated);
    }

    [Fact]
    public void GeneratedPartial_UsesDeclaringNamespaceAndClassName()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample.Deep.Nested;

            public partial class PersonViewModel : ViewModelBase
            {
                public PersonViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _name = string.Empty;
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "PersonViewModel");

        Assert.Contains("namespace Sample.Deep.Nested;", generated);
        Assert.Contains("partial class PersonViewModel", generated);
        Assert.Contains("#nullable enable", generated);
    }

    [Fact]
    public void PropertyName_StripsLeadingUnderscoreAndCapitalizes()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class NamingViewModel : ViewModelBase
            {
                public NamingViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private int _withUnderscore;
                [Binding] private int noUnderscore;
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "NamingViewModel");

        Assert.Contains("public int WithUnderscore", generated);
        Assert.Contains("get => _withUnderscore;", generated);
        Assert.Contains("public int NoUnderscore", generated);
        Assert.Contains("get => noUnderscore;", generated);
    }

    [Fact]
    public void PropertyName_CanBeOverriddenByTheAttribute()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class NamingViewModel : ViewModelBase
            {
                public NamingViewModel(IServiceProvider sp) : base(sp) { }

                [Binding(PropertyName = "DisplayName")] private string _name = string.Empty;
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "NamingViewModel");

        Assert.Contains("public string DisplayName", generated);
        Assert.Contains("get => _name;", generated);
        Assert.Contains("partial void OnDisplayNameChanged();", generated);
        Assert.DoesNotContain("public string Name", generated);
    }

    [Fact]
    public void FieldType_IsEmittedFullyQualified()
    {
        const string source = """
            using System.Collections.ObjectModel;
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class ListViewModel : ViewModelBase
            {
                public ListViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private ObservableCollection<string> _items = new();
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "ListViewModel");

        Assert.Contains("public System.Collections.ObjectModel.ObservableCollection<string> Items", generated);
    }

    [Fact]
    public void WritableField_DeclaresPartialOnChangedHook_AndInvokesIt()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class PersonViewModel : ViewModelBase
            {
                public PersonViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _name = string.Empty;
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "PersonViewModel");

        Assert.Contains("partial void OnNameChanged();", generated);
        Assert.Contains("OnNameChanged();", generated);
    }

    [Fact]
    public void ReadOnlyBinding_GeneratesGetterOnly_AndNoChangeHook()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class ReadOnlyViewModel : ViewModelBase
            {
                public ReadOnlyViewModel(IServiceProvider sp) : base(sp) { }

                [Binding(ReadOnly = true)] private string _id = "abc";
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "ReadOnlyViewModel");

        Assert.Contains("get => _id;", generated);
        Assert.DoesNotContain("SetProperty(ref _id", generated);
        Assert.DoesNotContain("partial void OnIdChanged();", generated);
    }

    [Fact]
    public void MultipleDeclaratorsOnOneField_GenerateOnePropertyEach()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class MultiViewModel : ViewModelBase
            {
                public MultiViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private int _first, _second;
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "MultiViewModel");

        Assert.Contains("public int First", generated);
        Assert.Contains("public int Second", generated);
    }

    [Fact]
    public void ClassWithoutBindingFields_ProducesNoOutput()
    {
        const string source = """
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class PlainViewModel : ViewModelBase
            {
                public PlainViewModel(IServiceProvider sp) : base(sp) { }

                public string Name { get; set; } = string.Empty;
            }
            """;

        Assert.Null(GeneratorTestHelper.TryGetBindingSource(source, "PlainViewModel"));
    }

    [Fact]
    public void CommandOnlyClass_ProducesNoOutput()
    {
        // The syntax filter accepts classes exposing RelayCommand properties, but without
        // [Binding] fields there is nothing to generate.
        const string source = """
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class CommandOnlyViewModel : ViewModelBase
            {
                public CommandOnlyViewModel(IServiceProvider sp) : base(sp) { }

                public RelayCommand SaveCommand => CreateCommand(Save);

                private void Save() { }
            }
            """;

        Assert.Null(GeneratorTestHelper.TryGetBindingSource(source, "CommandOnlyViewModel"));
    }

    [Fact]
    public void Generator_ReportsNoDiagnostics()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class PersonViewModel : ViewModelBase
            {
                public PersonViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _name = string.Empty;
            }
            """;

        GeneratorTestHelper.GeneratorResult result = GeneratorTestHelper.Run(source);

        Assert.Empty(result.Diagnostics);
    }
}
