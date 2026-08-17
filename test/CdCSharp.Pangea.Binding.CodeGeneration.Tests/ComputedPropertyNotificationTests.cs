namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// The feature that makes the ViewModels "refresh automatically": a setter must raise
/// PropertyChanged for every computed property that reads it, directly or transitively.
/// </summary>
public class ComputedPropertyNotificationTests
{
    [Fact]
    public void Setter_NotifiesComputedPropertyThatReadsIt()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class PersonViewModel : ViewModelBase
            {
                public PersonViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _firstName = string.Empty;
                [Binding] private string _lastName = string.Empty;

                public string FullName => $"{FirstName} {LastName}";
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "PersonViewModel");

        Assert.Equal(2, CountOccurrences(generated, "OnPropertyChanged(nameof(FullName));"));
    }

    [Fact]
    public void Setter_DoesNotNotifyUnrelatedComputedProperty()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class PersonViewModel : ViewModelBase
            {
                public PersonViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _firstName = string.Empty;
                [Binding] private int _age;

                public string Greeting => "Hi " + FirstName;
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "PersonViewModel");
        string ageSetter = ExtractPropertyBody(generated, "public int Age");

        Assert.DoesNotContain("Greeting", ageSetter);
    }

    [Fact]
    public void Setter_NotifiesTransitivelyDependentComputedProperty()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class PersonViewModel : ViewModelBase
            {
                public PersonViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _firstName = string.Empty;

                public string FullName => FirstName + "!";
                public string Display => FullName.ToUpperInvariant();
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "PersonViewModel");
        string firstNameSetter = ExtractPropertyBody(generated, "public string FirstName");

        Assert.Contains("OnPropertyChanged(nameof(FullName));", firstNameSetter);
        Assert.Contains("OnPropertyChanged(nameof(Display));", firstNameSetter);
    }

    [Fact]
    public void Setter_NotifiesComputedPropertyDeclaredWithGetterBlock()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class PersonViewModel : ViewModelBase
            {
                public PersonViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private int _age;

                public string Category
                {
                    get
                    {
                        return Age >= 18 ? "adult" : "minor";
                    }
                }
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "PersonViewModel");

        Assert.Contains("OnPropertyChanged(nameof(Category));", generated);
    }

    [Fact]
    public void ComputedNotifications_AreEmittedInAlphabeticalOrder()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class PersonViewModel : ViewModelBase
            {
                public PersonViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _name = string.Empty;

                public string Zeta => Name + "z";
                public string Alpha => Name + "a";
                public string Mid => Name + "m";
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "PersonViewModel");

        int alpha = generated.IndexOf("nameof(Alpha)", StringComparison.Ordinal);
        int mid = generated.IndexOf("nameof(Mid)", StringComparison.Ordinal);
        int zeta = generated.IndexOf("nameof(Zeta)", StringComparison.Ordinal);

        Assert.True(alpha >= 0 && mid >= 0 && zeta >= 0, "All computed notifications should be emitted.");
        Assert.True(alpha < mid && mid < zeta, "Notifications should be emitted alphabetically for stable output.");
    }

    [Fact]
    public void ReadOnlyBinding_ProducesNoNotifications()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class PersonViewModel : ViewModelBase
            {
                public PersonViewModel(IServiceProvider sp) : base(sp) { }

                [Binding(ReadOnly = true)] private string _id = "x";

                public string Display => Id + "!";
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "PersonViewModel");

        Assert.DoesNotContain("OnPropertyChanged(nameof(Display));", generated);
    }

    internal static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = text.IndexOf(value, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>
    /// Returns the generated text from <paramref name="declaration"/> up to the next generated
    /// property, so assertions can target a single setter.
    /// </summary>
    internal static string ExtractPropertyBody(string generated, string declaration)
    {
        int start = generated.IndexOf(declaration, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Property '{declaration}' was not generated.");

        int next = generated.IndexOf("    public ", start + declaration.Length, StringComparison.Ordinal);
        return next < 0 ? generated[start..] : generated[start..next];
    }
}
