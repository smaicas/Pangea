using static CdCSharp.Pangea.Binding.CodeGeneration.Tests.ComputedPropertyNotificationTests;

namespace CdCSharp.Pangea.Binding.CodeGeneration.Tests;

/// <summary>
/// When a property's <c>On{Property}Changed</c> hook mutates a collection, everything reading that
/// collection has to be refreshed as well - the generator wires those notifications too.
/// </summary>
public class CollectionNotificationTests
{
    private const string FilteredListViewModel = """
        using System.Collections.ObjectModel;
        using CdCSharp.Pangea.Binding.Attributes;
        using CdCSharp.Pangea.Core.Base;

        namespace Sample;

        public partial class FilteredListViewModel : ViewModelBase
        {
            public FilteredListViewModel(IServiceProvider sp) : base(sp) { }

            [Binding] private string _filter = string.Empty;

            public ObservableCollection<string> Items { get; } = new();

            public bool HasItems => Items.Count > 0;
            public int VisibleCount => Items.Count;

            partial void OnFilterChanged() => ApplyFilter();

            private void ApplyFilter()
            {
                Items.Clear();
                Items.Add(Filter);
            }
        }
        """;

    [Fact]
    public void PropertyHook_MutatingCollection_RefreshesCollectionDependentProperties()
    {
        string generated = GeneratorTestHelper.GetBindingSource(FilteredListViewModel, "FilteredListViewModel");
        string filterSetter = ExtractPropertyBody(generated, "public string Filter");

        Assert.Contains("OnPropertyChanged(nameof(HasItems));", filterSetter);
        Assert.Contains("OnPropertyChanged(nameof(VisibleCount));", filterSetter);
    }

    [Fact]
    public void CollectionNotifications_AreEmittedOnceEach()
    {
        string generated = GeneratorTestHelper.GetBindingSource(FilteredListViewModel, "FilteredListViewModel");

        Assert.Equal(1, CountOccurrences(generated, "OnPropertyChanged(nameof(HasItems));"));
        Assert.Equal(1, CountOccurrences(generated, "OnPropertyChanged(nameof(VisibleCount));"));
    }

    [Fact]
    public void PropertyHook_WithoutCollectionMutation_AddsNoExtraNotifications()
    {
        const string source = """
            using System.Collections.ObjectModel;
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class QuietViewModel : ViewModelBase
            {
                public QuietViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _filter = string.Empty;

                public ObservableCollection<string> Items { get; } = new();

                public bool HasItems => Items.Count > 0;

                partial void OnFilterChanged() => Log();

                private void Log() { }
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "QuietViewModel");

        Assert.DoesNotContain("OnPropertyChanged(nameof(HasItems));", generated);
    }

    [Theory]
    [InlineData("OnPropertyChanged(nameof(Summary));")]
    [InlineData("this.OnPropertyChanged(nameof(Summary));")]
    [InlineData("OnPropertyChanged(\"Summary\");")]
    public void ManualNotification_IsPropagated_RegardlessOfCallStyle(string notification)
    {
        string source = $$"""
            using System.Collections.ObjectModel;
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class ManualViewModel : ViewModelBase
            {
                public ManualViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _filter = string.Empty;

                public ObservableCollection<string> Items { get; } = new();

                public string Summary => "custom";

                partial void OnFilterChanged() => Rebuild();

                private void Rebuild()
                {
                    Items.Clear();
                    {{notification}}
                }
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "ManualViewModel");
        string filterSetter = ExtractPropertyBody(generated, "public string Filter");

        Assert.Contains("OnPropertyChanged(nameof(Summary));", filterSetter);
    }

    [Fact]
    public void ExpressionBodiedMutatingMethod_IsDetected()
    {
        const string source = """
            using System.Collections.ObjectModel;
            using CdCSharp.Pangea.Binding.Attributes;
            using CdCSharp.Pangea.Core.Base;

            namespace Sample;

            public partial class TerseViewModel : ViewModelBase
            {
                public TerseViewModel(IServiceProvider sp) : base(sp) { }

                [Binding] private string _filter = string.Empty;

                public ObservableCollection<string> Items { get; } = new();

                public bool HasItems => Items.Count > 0;

                partial void OnFilterChanged() => Reset();

                private void Reset() => Items.Clear();
            }
            """;

        string generated = GeneratorTestHelper.GetBindingSource(source, "TerseViewModel");
        string filterSetter = ExtractPropertyBody(generated, "public string Filter");

        Assert.Contains("OnPropertyChanged(nameof(HasItems));", filterSetter);
    }

}
