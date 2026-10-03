using Microsoft.CodeAnalysis;
using System.ComponentModel;
using System.Reflection;

namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// [Binding] on a model the UI edits in place, rather than on a screen.
/// </summary>
/// <remarks>
/// An application that only knew of ViewModelBase wrote its own notifying base for its models, with
/// a hand-kept list of computed properties. One property missing from that list showed up as a label
/// that only refreshed when something else redrew it - read by the user as "it did not save".
/// </remarks>
public class ObservableModelTests
{
    private const string Step = """
        using CdCSharp.Pangea.Binding.Attributes;
        using CdCSharp.Pangea.Core.Base;

        namespace Sample;

        public partial class MacroStep : ObservableModel
        {
            [Binding] private string _key = "A";
            [Binding] private int _repeat = 1;

            public string Summary => $"{Key} x{Repeat}";
            public bool IsRepeated => Repeat > 1;
        }
        """;

    [Fact]
    public void AModel_GetsGeneratedPropertiesWithoutAServiceProvider()
    {
        object step = Create(Step, "Sample.MacroStep");

        step.GetType().GetProperty("Key")!.SetValue(step, "B");

        Assert.Equal("B", step.GetType().GetProperty("Key")!.GetValue(step));
    }

    [Fact]
    public void AModelsComputedProperties_AreNotifiedFromWhatTheyRead()
    {
        object step = Create(Step, "Sample.MacroStep");
        List<string?> raised = Track(step);

        step.GetType().GetProperty("Key")!.SetValue(step, "B");

        Assert.Contains("Key", raised);
        Assert.Contains("Summary", raised);
        Assert.DoesNotContain("IsRepeated", raised);

        raised.Clear();
        step.GetType().GetProperty("Repeat")!.SetValue(step, 3);

        Assert.Contains("Summary", raised);
        Assert.Contains("IsRepeated", raised);
    }

    [Fact]
    public void AModel_ProducesNoDiagnostics()
    {
        GeneratorTestHelper.GeneratorResult result = GeneratorTestHelper.Run(Step);

        Assert.Empty(result.Diagnostics);
        Assert.NotEmpty(result.Sources);
    }

    /// <summary>
    /// The generated setter of a validated field calls ValidateProperty, which a model does not
    /// have. Without PGB007 the author gets a compiler error about a file they never wrote.
    /// </summary>
    [Fact]
    public void ValidationAttributesOnAModel_AreReportedAgainstTheField()
    {
        GeneratorTestHelper.GeneratorResult result = GeneratorTestHelper.Run("""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;
            using System.ComponentModel.DataAnnotations;

            namespace Sample;

            public partial class Contact : ObservableModel
            {
                [Binding, Required] private string _email = "";
                [Binding] private string _name = "";
            }
            """);

        Diagnostic reported = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id == "PGB007");

        Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
        Assert.Contains("_email", reported.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Contact", reported.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationAttributesOnAViewModel_AreNotReported()
    {
        GeneratorTestHelper.GeneratorResult result = GeneratorTestHelper.Run("""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;
            using System.ComponentModel.DataAnnotations;

            namespace Sample;

            public partial class ContactViewModel : ViewModelBase
            {
                public ContactViewModel(IServiceProvider sp) : base(sp) { }
                [Binding, Required] private string _email = "";
            }
            """);

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "PGB007");
    }

    private static object Create(string source, string typeName)
    {
        Assembly assembly = GeneratorTestHelper.RunAndLoad(source);
        Type type = assembly.GetType(typeName) ?? throw new InvalidOperationException($"{typeName} not found.");
        return Activator.CreateInstance(type)!;
    }

    private static List<string?> Track(object model)
    {
        List<string?> raised = new();
        ((INotifyPropertyChanged)model).PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }
}
