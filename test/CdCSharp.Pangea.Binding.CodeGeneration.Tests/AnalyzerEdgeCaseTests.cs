using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using System.ComponentModel;
using System.Reflection;

namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// The shapes a real view model takes, each run for real: the generated code is compiled, loaded
/// and exercised, and what is asserted is which notifications actually arrive.
/// </summary>
/// <remarks>
/// The analyzer is the toolkit's centre, and its failures are quiet - a property that never
/// refreshes looks like a binding problem, not a generator one. Emitted text cannot show that.
/// </remarks>
public class AnalyzerEdgeCaseTests
{
    private const string Header = """
        using CdCSharp.Pangea.Binding.Attributes;
        using CdCSharp.Pangea.Core.Base;
        using System.Collections.ObjectModel;

        namespace Sample;


        """;

    private sealed class Services : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IRelayCommandFactory) ? new RelayCommandFactory(null) : null;
    }

    private static object Build(string body, string typeName)
    {
        Assembly assembly = GeneratorTestHelper.RunAndLoad(Header + body);
        Type type = assembly.GetType("Sample." + typeName)!;

        Assert.True(type is not null, $"'{typeName}' was not produced.");

        return Activator.CreateInstance(type!, new Services())!;
    }

    private static List<string?> Track(object viewModel)
    {
        List<string?> raised = [];
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }

    private static void Set(object viewModel, string property, object? value) =>
        viewModel.GetType().GetProperty(property)!.SetValue(viewModel, value);

    private static object? Get(object viewModel, string property) =>
        viewModel.GetType().GetProperty(property)!.GetValue(viewModel);

    [Fact]
    public void ADependencyChainOfThreeHops_PropagatesAllTheWay()
    {
        object viewModel = Build("""
            public partial class ChainViewModel : ViewModelBase
            {
                public ChainViewModel(IServiceProvider s) : base(s) { }
                [Binding] private int _a;

                public int B => A * 2;
                public int C => B * 2;
                public int D => C * 2;
            }
            """, "ChainViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "A", 1);

        // The whole chain, and nothing beyond it.
        Assert.Equal("A,B,C,D", string.Join(",", raised.OrderBy(name => name, StringComparer.Ordinal)));
    }

    /// <summary>Two computed properties reading each other: the walk must terminate.</summary>
    [Fact]
    public void ACycleBetweenComputedProperties_DoesNotHangTheGenerator()
    {
        object viewModel = Build("""
            public partial class CycleViewModel : ViewModelBase
            {
                public CycleViewModel(IServiceProvider s) : base(s) { }
                [Binding] private int _seed;

                public int X => Y + Seed;
                public int Y => X + Seed;
            }
            """, "CycleViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "Seed", 1);

        Assert.Contains("Seed", raised);
    }

    [Fact]
    public void ARenamedProperty_IsWhatDependentsAreNotifiedThrough()
    {
        object viewModel = Build("""
            public partial class RenameViewModel : ViewModelBase
            {
                public RenameViewModel(IServiceProvider s) : base(s) { }
                [Binding(PropertyName = "Renamed")] private int _raw;

                public int Doubled => Renamed * 2;
            }
            """, "RenameViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "Renamed", 2);

        Assert.Contains("Renamed", raised);
        Assert.Contains("Doubled", raised);
    }

    [Fact]
    public void SeveralDeclaratorsOnOneField_EachBecomeAProperty()
    {
        object viewModel = Build("""
            public partial class MultiViewModel : ViewModelBase
            {
                public MultiViewModel(IServiceProvider s) : base(s) { }
                [Binding] private int _a, _b;

                public int Sum => A + B;
            }
            """, "MultiViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "A", 1);
        Set(viewModel, "B", 2);

        Assert.Contains("A", raised);
        Assert.Contains("B", raised);
        Assert.Equal(3, Get(viewModel, "Sum"));
    }

    [Fact]
    public void AComputedPropertyWithAGetterBlock_IsNotified()
    {
        object viewModel = Build("""
            public partial class BlockViewModel : ViewModelBase
            {
                public BlockViewModel(IServiceProvider s) : base(s) { }
                [Binding] private int _n;

                public string Label
                {
                    get
                    {
                        if (N > 0) return "positive";
                        return "other";
                    }
                }
            }
            """, "BlockViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "N", 5);

        Assert.Contains("Label", raised);
    }

    /// <summary>
    /// <c>Total =&gt; Compute()</c> reads its dependencies just as surely as one written inline.
    /// Commands and collections were always followed through a method; computed properties were not.
    /// </summary>
    [Fact]
    public void AComputedPropertyThatCallsAMethod_FollowsWhatTheMethodReads()
    {
        object viewModel = Build("""
            public partial class IndirectViewModel : ViewModelBase
            {
                public IndirectViewModel(IServiceProvider s) : base(s) { }
                [Binding] private int _v;

                public int Via => Compute();

                private int Compute() => V * 3;
            }
            """, "IndirectViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "V", 2);

        Assert.Contains("Via", raised);
        Assert.Equal(6, Get(viewModel, "Via"));
    }

    /// <summary>
    /// The documented pitfall, pinned so it stays a known limitation rather than drifting.
    /// </summary>
    [Fact]
    public void AComputedPropertyReadingTheBackingField_IsNotNotified()
    {
        object viewModel = Build("""
            public partial class FieldViewModel : ViewModelBase
            {
                public FieldViewModel(IServiceProvider s) : base(s) { }
                [Binding] private int _f;

                public int Bad => _f * 2;
            }
            """, "FieldViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "F", 3);

        Assert.Contains("F", raised);
        Assert.DoesNotContain("Bad", raised);
    }

    [Fact]
    public void AReadOnlyBinding_CanStillBeDependedOn()
    {
        object viewModel = Build("""
            public partial class ReadOnlyViewModel : ViewModelBase
            {
                public ReadOnlyViewModel(IServiceProvider s) : base(s) { }
                [Binding(ReadOnly = true)] private int _r = 2;
                [Binding] private int _m;

                public int Prod => R * M;
            }
            """, "ReadOnlyViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "M", 4);

        Assert.Contains("Prod", raised);
        Assert.Equal(8, Get(viewModel, "Prod"));
    }

    /// <summary>
    /// A screen deriving from a shared view model, with nothing of its own to generate. The base
    /// raises its property and cannot know what the subclass built on it.
    /// </summary>
    [Fact]
    public void ACommandDependingOnAnInheritedProperty_IsReEvaluated()
    {
        object viewModel = Build("""
            public partial class SharedBase : ViewModelBase
            {
                public SharedBase(IServiceProvider s) : base(s) { }
                [Binding] private bool _ready;
            }

            public partial class InheritViewModel : SharedBase
            {
                public InheritViewModel(IServiceProvider s) : base(s) { }

                public RelayCommand Go => CreateCommand(() => { }, () => Ready);
            }
            """, "InheritViewModel");

        RelayCommand command = (RelayCommand)Get(viewModel, "Go")!;

        int raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        Assert.False(command.CanExecute(null));

        Set(viewModel, "Ready", true);

        Assert.True(raised > 0, "The command was never told its CanExecute might have changed.");
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void ACommandWhoseCanExecuteIsAMethod_IsReEvaluated()
    {
        object viewModel = Build("""
            public partial class MethodViewModel : ViewModelBase
            {
                public MethodViewModel(IServiceProvider s) : base(s) { }
                [Binding] private bool _ready;

                public RelayCommand Go => CreateCommand(() => { }, CanGo);

                private bool CanGo() => Ready;
            }
            """, "MethodViewModel");

        RelayCommand command = (RelayCommand)Get(viewModel, "Go")!;

        int raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        Set(viewModel, "Ready", true);

        Assert.True(raised > 0);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void ACommandWhoseCanExecuteIsAComputedProperty_IsReEvaluated()
    {
        object viewModel = Build("""
            public partial class ComputedViewModel : ViewModelBase
            {
                public ComputedViewModel(IServiceProvider s) : base(s) { }
                [Binding] private int _n;

                public bool IsOk => N > 0;

                public RelayCommand Go => CreateCommand(() => { }, () => IsOk);
            }
            """, "ComputedViewModel");

        RelayCommand command = (RelayCommand)Get(viewModel, "Go")!;

        int raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        Set(viewModel, "N", 5);

        Assert.True(raised > 0);
        Assert.True(command.CanExecute(null));
    }
}
