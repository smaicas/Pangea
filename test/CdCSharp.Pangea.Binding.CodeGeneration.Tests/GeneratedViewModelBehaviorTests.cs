using CdCSharp.Pangea.Core.Base;
using System.ComponentModel;
using System.Reflection;

namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// End-to-end checks: the generated code is compiled and executed, so the tests verify the
/// observable behaviour a consumer actually gets, not just the emitted text.
/// </summary>
public class GeneratedViewModelBehaviorTests
{
    private const string OrderViewModel = """
        using CdCSharp.Pangea.Binding.Attributes;
        using CdCSharp.Pangea.Core.Base;

        namespace Sample;

        public partial class OrderViewModel : ViewModelBase
        {
            public OrderViewModel(IServiceProvider sp) : base(sp)
            {
                CheckoutCommand = CreateCommand(Checkout, () => CanCheckout);
            }

            [Binding] private int _quantity;
            [Binding] private decimal _unitPrice;
            [Binding(ReadOnly = true)] private string _orderId = "ORD-1";

            public decimal Total => Quantity * UnitPrice;
            public bool CanCheckout => Total > 0;

            public RelayCommand CheckoutCommand { get; }

            public int CheckoutCount { get; private set; }

            private void Checkout() => CheckoutCount++;
        }
        """;

    /// <summary>
    /// The same view model in the shape the README, the agent skill and every sample use: commands
    /// as expression-bodied properties.
    /// </summary>
    /// <remarks>
    /// Worth its own fixture because the styles are not equivalent. An expression-bodied property
    /// is re-evaluated on every read, so a command built there is a different object each time
    /// unless the base class keeps one - and the whole of CanExecute propagation depends on the
    /// binding and the generated RaiseCanExecuteChanged call reaching the same instance. The suite
    /// only ever exercised the constructor style, which is why that gap survived.
    /// </remarks>
    private const string ExpressionBodiedOrderViewModel = """
        using CdCSharp.Pangea.Binding.Attributes;
        using CdCSharp.Pangea.Core.Base;

        namespace Sample;

        public partial class OrderViewModel : ViewModelBase
        {
            public OrderViewModel(IServiceProvider sp) : base(sp) { }

            [Binding] private int _quantity;
            [Binding] private decimal _unitPrice;

            public decimal Total => Quantity * UnitPrice;
            public bool CanCheckout => Total > 0;

            public RelayCommand CheckoutCommand => CreateCommand(Checkout, () => CanCheckout);
            public RelayCommand ResetCommand => CreateCommand(Reset);

            public int CheckoutCount { get; private set; }

            private void Checkout() => CheckoutCount++;
            private void Reset() { }
        }
        """;

    [Fact]
    public void ExpressionBodiedCommand_IsTheSameInstanceOnEveryRead()
    {
        object viewModel = CreateViewModel(ExpressionBodiedOrderViewModel, "Sample.OrderViewModel");

        Assert.Same(GetProperty(viewModel, "CheckoutCommand"), GetProperty(viewModel, "CheckoutCommand"));
    }

    [Fact]
    public void ExpressionBodiedCommands_AreNotSharedWithEachOther()
    {
        object viewModel = CreateViewModel(ExpressionBodiedOrderViewModel, "Sample.OrderViewModel");

        Assert.NotSame(GetProperty(viewModel, "CheckoutCommand"), GetProperty(viewModel, "ResetCommand"));
    }

    /// <summary>
    /// The failure a user sees: a button bound once, and a CanExecute that never refreshes.
    /// </summary>
    [Fact]
    public void ExpressionBodiedCommand_HearsTheGeneratedCanExecuteChanged()
    {
        object viewModel = CreateViewModel(ExpressionBodiedOrderViewModel, "Sample.OrderViewModel");

        // Read once, exactly as a binding does, and hold on to it.
        RelayCommand bound = (RelayCommand)GetProperty(viewModel, "CheckoutCommand")!;

        int raised = 0;
        bound.CanExecuteChanged += (_, _) => raised++;

        Assert.False(bound.CanExecute(null));

        SetProperty(viewModel, "Quantity", 2);
        SetProperty(viewModel, "UnitPrice", 5m);

        Assert.True(raised >= 2, $"The bound command was notified {raised} time(s).");
        Assert.True(bound.CanExecute(null));
    }

    [Fact]
    public void ExpressionBodiedCommand_Executes()
    {
        object viewModel = CreateViewModel(ExpressionBodiedOrderViewModel, "Sample.OrderViewModel");
        RelayCommand bound = (RelayCommand)GetProperty(viewModel, "CheckoutCommand")!;

        SetProperty(viewModel, "Quantity", 1);
        SetProperty(viewModel, "UnitPrice", 3m);

        bound.Execute(null);

        Assert.Equal(1, GetProperty(viewModel, "CheckoutCount"));
    }

    [Fact]
    public void GeneratedCode_Compiles()
    {
        // RunAndLoad asserts there are no compilation errors.
        Assembly assembly = GeneratorTestHelper.RunAndLoad(OrderViewModel);

        Assert.NotNull(assembly.GetType("Sample.OrderViewModel"));
    }

    [Fact]
    public void SettingProperty_RaisesPropertyChangedForPropertyAndDependents()
    {
        object viewModel = CreateViewModel(OrderViewModel, "Sample.OrderViewModel");
        List<string?> raised = TrackPropertyChanged(viewModel);

        SetProperty(viewModel, "Quantity", 3);

        Assert.Contains("Quantity", raised);
        Assert.Contains("Total", raised);
        Assert.Contains("CanCheckout", raised);
    }

    [Fact]
    public void SettingProperty_ToSameValue_RaisesNothing()
    {
        object viewModel = CreateViewModel(OrderViewModel, "Sample.OrderViewModel");
        SetProperty(viewModel, "Quantity", 3);

        List<string?> raised = TrackPropertyChanged(viewModel);
        SetProperty(viewModel, "Quantity", 3);

        Assert.Empty(raised);
    }

    [Fact]
    public void ComputedProperty_ReflectsTheNewValue()
    {
        object viewModel = CreateViewModel(OrderViewModel, "Sample.OrderViewModel");

        SetProperty(viewModel, "Quantity", 4);
        SetProperty(viewModel, "UnitPrice", 2.5m);

        Assert.Equal(10m, GetProperty(viewModel, "Total"));
    }

    [Fact]
    public void ReadOnlyBinding_ExposesGetterAndNoSetter()
    {
        object viewModel = CreateViewModel(OrderViewModel, "Sample.OrderViewModel");
        PropertyInfo property = viewModel.GetType().GetProperty("OrderId")!;

        Assert.NotNull(property);
        Assert.Null(property.SetMethod);
        Assert.Equal("ORD-1", property.GetValue(viewModel));
    }

    [Fact]
    public void SettingProperty_RaisesCanExecuteChangedOnDependentCommand()
    {
        object viewModel = CreateViewModel(OrderViewModel, "Sample.OrderViewModel");
        RelayCommand command = (RelayCommand)GetProperty(viewModel, "CheckoutCommand")!;

        int raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        Assert.False(command.CanExecute(null));

        SetProperty(viewModel, "Quantity", 2);
        SetProperty(viewModel, "UnitPrice", 5m);

        Assert.True(raised >= 2);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void PartialOnChangedHook_IsInvokedBySetter()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class HookViewModel : ViewModelBase
            {
                public HookViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _name = string.Empty;

                public int HookCalls { get; private set; }

                partial void OnNameChanged() => HookCalls++;
            }
            """;

        object viewModel = CreateViewModel(source, "Sample.HookViewModel");

        SetProperty(viewModel, "Name", "a");
        SetProperty(viewModel, "Name", "b");
        SetProperty(viewModel, "Name", "b");

        Assert.Equal(2, GetProperty(viewModel, "HookCalls"));
    }

    [Fact]
    public void CollectionHook_RefreshesDependentProperties()
    {
        const string source = """
            using System.Collections.ObjectModel;
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class SearchViewModel : ViewModelBase
            {
                public SearchViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _query = string.Empty;

                public ObservableCollection<string> Results { get; } = new();

                public bool HasResults => Results.Count > 0;

                partial void OnQueryChanged() => Search();

                private void Search()
                {
                    Results.Clear();
                    Results.Add(Query);
                }
            }
            """;

        object viewModel = CreateViewModel(source, "Sample.SearchViewModel");
        List<string?> raised = TrackPropertyChanged(viewModel);

        SetProperty(viewModel, "Query", "avalonia");

        Assert.Contains("Query", raised);
        Assert.Contains("HasResults", raised);
        Assert.True((bool)GetProperty(viewModel, "HasResults")!);
    }

    /// <summary>
    /// A shared base view model with the common fields, a screen deriving from it and computing
    /// from them. The setter that raises the inherited property lives in the base class, which
    /// cannot know what a subclass derived from it.
    /// </summary>
    [Fact]
    public void AComputedPropertyDependingOnAnInheritedBinding_IsStillNotified()
    {
        const string source = """
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class SharedViewModel : ViewModelBase
            {
                public SharedViewModel(IServiceProvider sp) : base(sp) { }
                [Binding] private int _quantity;
            }

            public partial class ScreenViewModel : SharedViewModel
            {
                public ScreenViewModel(IServiceProvider sp) : base(sp) { }
                [Binding] private decimal _unitPrice;

                public decimal Total => Quantity * UnitPrice;
            }
            """;

        object viewModel = CreateViewModel(source, "Sample.ScreenViewModel");
        List<string?> raised = TrackPropertyChanged(viewModel);

        // Declared here: this has always worked.
        SetProperty(viewModel, "UnitPrice", 5m);
        Assert.Contains("Total", raised);

        raised.Clear();

        // Declared by the base: silently missed before, because nothing connected the two.
        SetProperty(viewModel, "Quantity", 3);

        Assert.Contains("Quantity", raised);
        Assert.Contains("Total", raised);
        Assert.Equal(15m, GetProperty(viewModel, "Total"));
    }

    /// <summary>A class that inherits nothing it depends on gets no forwarding at all.</summary>
    [Fact]
    public void AViewModelWithNoInheritedDependencies_DoesNotOverrideOnPropertyChanged()
    {
        GeneratorTestHelper.GeneratorResult result = GeneratorTestHelper.Run("""
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class PlainViewModel : ViewModelBase
            {
                public PlainViewModel(IServiceProvider sp) : base(sp) { }
                [Binding] private int _count;

                public int Doubled => Count * 2;
            }
            """);

        string generated = string.Join("\n", result.Sources.Select(source => source.Text));

        Assert.DoesNotContain("override void OnPropertyChanged", generated, StringComparison.Ordinal);
    }

    private static object CreateViewModel(string source, string typeName)
    {
        Assembly assembly = GeneratorTestHelper.RunAndLoad(source);
        Type type = assembly.GetType(typeName) ?? throw new InvalidOperationException($"{typeName} not found.");
        return Activator.CreateInstance(type, new TestServiceProvider())!;
    }

    private static List<string?> TrackPropertyChanged(object viewModel)
    {
        List<string?> raised = new();
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }

    private static void SetProperty(object viewModel, string name, object? value) =>
        viewModel.GetType().GetProperty(name)!.SetValue(viewModel, value);

    private static object? GetProperty(object viewModel, string name) =>
        viewModel.GetType().GetProperty(name)!.GetValue(viewModel);
}
