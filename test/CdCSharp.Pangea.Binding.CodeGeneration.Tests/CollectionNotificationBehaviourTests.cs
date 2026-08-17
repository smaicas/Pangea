using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;

namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// A change hook that fills a collection, and the properties that read it.
/// </summary>
/// <remarks>
/// Only one shape of this was ever recognised - a hook delegating to a helper that mutates - which
/// is how the documentation happens to phrase it. Writing the same thing any other way notified
/// nothing, silently: the list filled and the view kept showing the old count.
/// </remarks>
public class CollectionNotificationBehaviourTests
{
    private const string Header = """
        using CdCSharp.Pangea.Binding.Attributes;
        using CdCSharp.Pangea.Core.Base;
        using System.Collections.Generic;
        using System.Collections.ObjectModel;
        using System.Linq;

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
        return Activator.CreateInstance(assembly.GetType("Sample." + typeName)!, new Services())!;
    }

    private static List<string?> Track(object viewModel)
    {
        List<string?> raised = [];
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        return raised;
    }

    private static void Set(object viewModel, string property, object? value) =>
        viewModel.GetType().GetProperty(property)!.SetValue(viewModel, value);

    [Fact]
    public void AHookThatDelegatesToAHelper_NotifiesWhatReadsTheCollection()
    {
        object viewModel = Build("""
            public partial class HelperViewModel : ViewModelBase
            {
                public HelperViewModel(IServiceProvider s) : base(s) { }
                [Binding] private string _query = "";

                public ObservableCollection<string> Results { get; } = new();
                public bool HasResults => Results.Count > 0;

                partial void OnQueryChanged() => Search();

                private void Search()
                {
                    Results.Clear();
                    Results.Add(Query);
                }
            }
            """, "HelperViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "Query", "avalonia");

        Assert.Contains("HasResults", raised);
    }

    /// <summary>The same thing without the helper. Nothing about it is different to a reader.</summary>
    [Fact]
    public void AHookThatMutatesDirectly_NotifiesWhatReadsTheCollection()
    {
        object viewModel = Build("""
            public partial class DirectViewModel : ViewModelBase
            {
                public DirectViewModel(IServiceProvider s) : base(s) { }
                [Binding] private string _query = "";

                public ObservableCollection<string> Results { get; } = new();
                public bool HasResults => Results.Count > 0;

                partial void OnQueryChanged()
                {
                    Results.Clear();
                    Results.Add(Query);
                }
            }
            """, "DirectViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "Query", "avalonia");

        Assert.Contains("HasResults", raised);
    }

    [Fact]
    public void AHookThatReachesTheCollectionThroughTwoMethods_StillNotifies()
    {
        object viewModel = Build("""
            public partial class TransitiveViewModel : ViewModelBase
            {
                public TransitiveViewModel(IServiceProvider s) : base(s) { }
                [Binding] private string _query = "";

                public ObservableCollection<string> Results { get; } = new();
                public bool HasResults => Results.Count > 0;

                partial void OnQueryChanged() => Outer();

                private void Outer() => Inner();
                private void Inner() => Results.Add(Query);
            }
            """, "TransitiveViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "Query", "avalonia");

        Assert.Contains("HasResults", raised);
    }

    /// <summary>
    /// The mechanism is the hook, not the collection type: a plain list works the same way.
    /// </summary>
    [Fact]
    public void APlainListWorksTheSameAsAnObservableCollection()
    {
        object viewModel = Build("""
            public partial class PlainListViewModel : ViewModelBase
            {
                public PlainListViewModel(IServiceProvider s) : base(s) { }
                [Binding] private string _query = "";

                public List<string> Results { get; } = new();
                public int Count => Results.Count;

                partial void OnQueryChanged() => Results.Add(Query);
            }
            """, "PlainListViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "Query", "avalonia");

        Assert.Contains("Count", raised);
    }

    /// <summary>Precision, not enthusiasm: the untouched collection's reader stays quiet.</summary>
    [Fact]
    public void OnlyTheCollectionThatWasMutated_HasItsReadersNotified()
    {
        object viewModel = Build("""
            public partial class TwoViewModel : ViewModelBase
            {
                public TwoViewModel(IServiceProvider s) : base(s) { }
                [Binding] private string _query = "";

                public ObservableCollection<string> Left { get; } = new();
                public ObservableCollection<string> Right { get; } = new();

                public int LeftCount => Left.Count;
                public int RightCount => Right.Count;

                partial void OnQueryChanged() => Left.Add(Query);
            }
            """, "TwoViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "Query", "avalonia");

        Assert.Contains("LeftCount", raised);
        Assert.DoesNotContain("RightCount", raised);
    }

    [Fact]
    public void ALinqExpressionOverTheCollection_CountsAsReadingIt()
    {
        object viewModel = Build("""
            public partial class LinqViewModel : ViewModelBase
            {
                public LinqViewModel(IServiceProvider s) : base(s) { }
                [Binding] private string _query = "";

                public ObservableCollection<string> Results { get; } = new();
                public bool Any => Results.Any();

                partial void OnQueryChanged() => Results.Add(Query);
            }
            """, "LinqViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "Query", "avalonia");

        Assert.Contains("Any", raised);
    }

    /// <summary>A collection that is itself a binding property: replacing it is an ordinary set.</summary>
    [Fact]
    public void ABoundCollectionProperty_NotifiesItsReadersWhenReplaced()
    {
        object viewModel = Build("""
            public partial class BoundCollectionViewModel : ViewModelBase
            {
                public BoundCollectionViewModel(IServiceProvider s) : base(s) { }
                [Binding] private ObservableCollection<string> _items = new();

                public int ItemCount => Items.Count;
            }
            """, "BoundCollectionViewModel");

        List<string?> raised = Track(viewModel);
        Set(viewModel, "Items", new ObservableCollection<string> { "a", "b" });

        Assert.Contains("Items", raised);
        Assert.Contains("ItemCount", raised);
    }
}
