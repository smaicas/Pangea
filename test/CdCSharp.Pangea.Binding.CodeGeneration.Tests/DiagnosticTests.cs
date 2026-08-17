using Microsoft.CodeAnalysis;

namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// What the generator says when it cannot do what was asked.
/// </summary>
/// <remarks>
/// Every one of these used to reach the author as a compiler error about the generated file -
/// "the name 'SetProperty' does not exist", "already contains a definition for 'Count'" - naming
/// code they never wrote. One case said nothing at all.
/// </remarks>
public class DiagnosticTests
{
    private static IReadOnlyList<Diagnostic> Diagnose(string body)
    {
        string source = $$"""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace App;

            {{body}}
            """;

        return GeneratorTestHelper.Run(source).Diagnostics;
    }

    private static Diagnostic Single(IReadOnlyList<Diagnostic> diagnostics, string id)
    {
        Diagnostic? found = diagnostics.FirstOrDefault(diagnostic => diagnostic.Id == id);

        Assert.True(found is not null,
            $"Expected {id}. Reported: " +
            (diagnostics.Count == 0 ? "(nothing)" : string.Join(", ", diagnostics.Select(d => d.Id))));

        return found!;
    }

    [Fact]
    public void ForgettingPartial_IsReportedAgainstTheClass()
    {
        IReadOnlyList<Diagnostic> diagnostics = Diagnose("""
            public class ForgotPartialViewModel : ViewModelBase
            {
                public ForgotPartialViewModel(IServiceProvider sp) : base(sp) { }
                [Binding] private int _count;
            }
            """);

        Diagnostic reported = Single(diagnostics, "PGB001");

        Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
        Assert.Contains("ForgotPartialViewModel", reported.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("partial", reported.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void NotDerivingFromViewModelBase_IsReported()
    {
        IReadOnlyList<Diagnostic> diagnostics = Diagnose("""
            public partial class OrphanViewModel
            {
                [Binding] private int _count;
            }
            """);

        Diagnostic reported = Single(diagnostics, "PGB002");

        Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
        Assert.Contains("ViewModelBase", reported.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void TwoFieldsProducingTheSameProperty_AreReported()
    {
        IReadOnlyList<Diagnostic> diagnostics = Diagnose("""
            public partial class ClashViewModel : ViewModelBase
            {
                public ClashViewModel(IServiceProvider sp) : base(sp) { }
                [Binding] private int _count;
                [Binding(PropertyName = "Count")] private int _other;
            }
            """);

        Diagnostic reported = Single(diagnostics, "PGB003");

        Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
        Assert.Contains("Count", reported.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("_other", reported.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void APropertyNameTheClassAlreadyDeclares_IsReported()
    {
        IReadOnlyList<Diagnostic> diagnostics = Diagnose("""
            public partial class TakenViewModel : ViewModelBase
            {
                public TakenViewModel(IServiceProvider sp) : base(sp) { }
                [Binding(PropertyName = "Total")] private int _count;
                public int Total => 1;
            }
            """);

        Diagnostic reported = Single(diagnostics, "PGB004");

        Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
        Assert.Contains("Total", reported.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    /// PGB002 is an error, so a false positive breaks a build that was compiling. It asks whether
    /// the members the generated code calls are inherited, not whether the base is named
    /// ViewModelBase: an application with its own notifying base is entitled to use [Binding].
    /// </summary>
    [Fact]
    public void AnApplicationsOwnNotifyingBase_IsAccepted()
    {
        GeneratorTestHelper.GeneratorResult result = GeneratorTestHelper.Run("""
            using CdCSharp.Pangea.Binding.Attributes;
            using System.Collections.Generic;
            using System.ComponentModel;
            using System.Runtime.CompilerServices;

            namespace App;

            public abstract class MyOwnBase : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;

                protected virtual void OnPropertyChanged([CallerMemberName] string? name = null) =>
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

                protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
                {
                    if (EqualityComparer<T>.Default.Equals(field, value)) return false;
                    field = value;
                    OnPropertyChanged(name);
                    return true;
                }
            }

            public partial class CustomViewModel : MyOwnBase
            {
                [Binding] private int _count;
            }
            """);

        Assert.Empty(result.Diagnostics);
        Assert.NotEmpty(result.Sources);
    }

    /// <summary>
    /// Adding HasErrors to ViewModelBase collided with a view model that already had one. The
    /// compiler does report it - against the generated file, about code the author cannot edit.
    /// </summary>
    [Fact]
    public void APropertyThatHidesABaseMember_IsReportedAgainstTheField()
    {
        IReadOnlyList<Diagnostic> diagnostics = Diagnose("""
            public partial class ShadowViewModel : ViewModelBase
            {
                public ShadowViewModel(IServiceProvider sp) : base(sp) { }
                [Binding] private bool _hasErrors;
            }
            """);

        Diagnostic reported = Single(diagnostics, "PGB006");

        Assert.Equal(DiagnosticSeverity.Warning, reported.Severity);
        Assert.Contains("_hasErrors", reported.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("HasErrors", reported.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("ViewModelBase", reported.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>The one that said nothing at all: the attribute was simply ignored.</summary>
    [Fact]
    public void ABindingOnAStaticField_IsReportedAsIgnored()
    {
        IReadOnlyList<Diagnostic> diagnostics = Diagnose("""
            public partial class StaticViewModel : ViewModelBase
            {
                public StaticViewModel(IServiceProvider sp) : base(sp) { }
                [Binding] private static int _count;
            }
            """);

        Diagnostic reported = Single(diagnostics, "PGB005");

        Assert.Equal(DiagnosticSeverity.Warning, reported.Severity);
        Assert.Contains("_count", reported.GetMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    /// One message the author can act on, instead of it plus a pile about a file they cannot edit.
    /// </summary>
    [Fact]
    public void WhenSomethingIsWrong_NoCodeIsGeneratedOnTopOfIt()
    {
        GeneratorTestHelper.GeneratorResult result = GeneratorTestHelper.Run("""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace App;

            public partial class OrphanViewModel
            {
                [Binding] private int _count;
            }
            """);

        Assert.Empty(result.Sources);
    }

    [Fact]
    public void AWellFormedViewModel_IsReportedClean()
    {
        IReadOnlyList<Diagnostic> diagnostics = Diagnose("""
            public partial class FineViewModel : ViewModelBase
            {
                public FineViewModel(IServiceProvider sp) : base(sp) { }
                [Binding] private int _count;
            }
            """);

        Assert.Empty(diagnostics);
    }
}
