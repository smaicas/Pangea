using Microsoft.CodeAnalysis;

namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// Type shapes an ordinary project contains: the same class name in two feature namespaces, a view
/// model nested in another type, a generic one.
/// </summary>
/// <remarks>
/// The generator names its output files after the type it generated for. Naming them after the
/// simple name made a repeated class name a duplicate file name, and Roslyn answers that by
/// abandoning the generator for the whole compilation - reported as CS8785, a warning - so every
/// <c>[Binding]</c> in the project silently stopped producing a property.
/// </remarks>
public class GeneratorTypeShapeTests
{
    private static GeneratorTestHelper.GeneratorResult Generate(string source)
    {
        GeneratorTestHelper.GeneratorResult result = GeneratorTestHelper.Run(source);

        Assert.True(result.Diagnostics.Count == 0,
            "The generator itself failed: " +
            string.Join(" | ", result.Diagnostics.Select(d => d.Id + " " + d.GetMessage())));

        string[] errors = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Id + ": " + diagnostic.GetMessage())
            .Distinct()
            .ToArray();

        Assert.True(errors.Length == 0, "The generated code does not compile: " + string.Join(" | ", errors));

        return result;
    }

    [Fact]
    public void TheSameClassNameInTwoNamespaces_BothGetGenerated()
    {
        GeneratorTestHelper.GeneratorResult result = Generate("""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace App.Orders
            {
                public partial class DetailViewModel : ViewModelBase
                {
                    public DetailViewModel(IServiceProvider sp) : base(sp) { }
                    [Binding] private string _orderCode = "";
                }
            }

            namespace App.Customers
            {
                public partial class DetailViewModel : ViewModelBase
                {
                    public DetailViewModel(IServiceProvider sp) : base(sp) { }
                    [Binding] private string _customerCode = "";
                }
            }
            """);

        string generated = string.Join("\n", result.Sources.Select(source => source.Text));

        Assert.Contains("public string OrderCode", generated, StringComparison.Ordinal);
        Assert.Contains("public string CustomerCode", generated, StringComparison.Ordinal);
    }

    /// <summary>The root cause, guarded directly: file names have to tell the two apart.</summary>
    [Fact]
    public void GeneratedFileNames_AreQualifiedByNamespace()
    {
        GeneratorTestHelper.GeneratorResult result = Generate("""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace App.Orders;

            public partial class DetailViewModel : ViewModelBase
            {
                public DetailViewModel(IServiceProvider sp) : base(sp) { }
                [Binding] private string _orderCode = "";
            }
            """);

        Assert.Contains(result.Sources, source => source.HintName.StartsWith("App.Orders.DetailViewModel", StringComparison.Ordinal));
    }

    [Fact]
    public void ANestedViewModel_IsGeneratedInsideItsContainer()
    {
        GeneratorTestHelper.GeneratorResult result = Generate("""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace App;

            public partial class Shell
            {
                public partial class InnerViewModel : ViewModelBase
                {
                    public InnerViewModel(IServiceProvider sp) : base(sp) { }
                    [Binding] private int _count;
                }
            }
            """);

        string generated = string.Join("\n", result.Sources.Select(source => source.Text));

        // Re-opened inside Shell, or it would describe a different top-level type of the same name.
        Assert.Contains("partial class Shell", generated, StringComparison.Ordinal);
        Assert.Contains("public int Count", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void AGenericViewModel_KeepsItsTypeParameters()
    {
        GeneratorTestHelper.GeneratorResult result = Generate("""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace App;

            public partial class ListViewModel<T> : ViewModelBase
            {
                public ListViewModel(IServiceProvider sp) : base(sp) { }
                [Binding] private int _count;
            }
            """);

        string generated = string.Join("\n", result.Sources.Select(source => source.Text));

        Assert.Contains("partial class ListViewModel<T>", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void AViewModelNestedTwoDeep_IsStillReached()
    {
        GeneratorTestHelper.GeneratorResult result = Generate("""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace App;

            public partial class Outer
            {
                public partial class Middle
                {
                    public partial class DeepViewModel : ViewModelBase
                    {
                        public DeepViewModel(IServiceProvider sp) : base(sp) { }
                        [Binding] private int _count;
                    }
                }
            }
            """);

        Assert.Contains(result.Sources,
            source => source.HintName.StartsWith("App.Outer.Middle.DeepViewModel", StringComparison.Ordinal));
    }

    /// <summary>
    /// One class, two declarations - what <c>partial</c> is for, and what the toolkit requires of
    /// every view model.
    /// </summary>
    /// <remarks>
    /// Generating per declaration wrote the same file name twice, and Roslyn answers that by
    /// dropping the generator for the whole compilation.
    /// </remarks>
    [Fact]
    public void APartialClassSplitAcrossDeclarations_IsGeneratedOnce()
    {
        GeneratorTestHelper.GeneratorResult result = Generate("""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace App;

            public partial class SplitViewModel : ViewModelBase
            {
                public SplitViewModel(IServiceProvider sp) : base(sp) { }
                [Binding] private int _quantity;
            }

            public partial class SplitViewModel
            {
                [Binding] private decimal _unitPrice;
            }
            """);

        Assert.Single(result.Sources, source => source.HintName.EndsWith(".Binding.g.cs", StringComparison.Ordinal));

        string generated = string.Join("\n", result.Sources.Select(source => source.Text));

        // Both halves, in the one file.
        Assert.Contains("public int Quantity", generated, StringComparison.Ordinal);
        Assert.Contains("public decimal UnitPrice", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reason generating per declaration was wrong even before the file names collided: each
    /// run only saw its own half, so a dependency across the split went unnoticed.
    /// </summary>
    [Fact]
    public void ADependencyAcrossTheSplit_IsStillNoticed()
    {
        GeneratorTestHelper.GeneratorResult result = Generate("""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace App;

            public partial class OrderViewModel : ViewModelBase
            {
                public OrderViewModel(IServiceProvider sp) : base(sp) { }
                [Binding] private int _quantity;
            }

            public partial class OrderViewModel
            {
                public int Doubled => Quantity * 2;
            }
            """);

        string generated = string.Join("\n", result.Sources.Select(source => source.Text));

        Assert.Contains("OnPropertyChanged(nameof(Doubled))", generated, StringComparison.Ordinal);
    }

    /// <summary>The fields can live in a later file than the one that opens the class.</summary>
    [Fact]
    public void TheBindingFieldsMayBeInTheSecondDeclaration()
    {
        GeneratorTestHelper.GeneratorResult result = Generate("""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace App;

            public partial class LateViewModel : ViewModelBase
            {
                public LateViewModel(IServiceProvider sp) : base(sp) { }
            }

            public partial class LateViewModel
            {
                [Binding] private int _count;
            }
            """);

        string generated = string.Join("\n", result.Sources.Select(source => source.Text));

        Assert.Contains("public int Count", generated, StringComparison.Ordinal);
    }
}
