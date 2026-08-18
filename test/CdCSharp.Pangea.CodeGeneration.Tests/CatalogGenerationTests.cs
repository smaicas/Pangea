namespace CdCSharp.Pangea.CodeGeneration.Tests;

/// <summary>
/// What the catalog says about an assembly, and what it deliberately does not.
/// </summary>
/// <remarks>
/// The catalog replaces an assembly scan, so every rule here has to match the one the scan
/// applies. A catalog that disagrees is worse than no catalog: startup would silently register a
/// different set of types from the one the application was written expecting.
/// </remarks>
public class CatalogGenerationTests
{
    [Fact]
    public void AFeature_IsBuiltByAConstructorCallRatherThanByActivator()
    {
        const string source = """
            using CdCSharp.Pangea.Core.Abstractions;
            using Microsoft.Extensions.DependencyInjection;
            using System;

            namespace Sample;

            public class OrdersFeature : IPangeaFeature
            {
                public string Name => "Orders";
                public Version Version => new(1, 0, 0);
                public void ConfigureServices(IServiceCollection services) { }
            }
            """;

        string catalog = CatalogTestHelper.RunExpectingCatalog(source);

        Assert.Contains("static () => new global::Sample.OrdersFeature()", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void AFeatureWithoutAParameterlessConstructor_IsLeftOut()
    {
        const string source = """
            using CdCSharp.Pangea.Core.Abstractions;
            using Microsoft.Extensions.DependencyInjection;
            using System;

            namespace Sample;

            public class NeedsSomething : IPangeaFeature
            {
                public NeedsSomething(string name) => Name = name;
                public string Name { get; }
                public Version Version => new(1, 0, 0);
                public void ConfigureServices(IServiceCollection services) { }
            }
            """;

        Assert.Null(CatalogTestHelper.Run(source));
    }

    /// <summary>
    /// The shape every view model in the toolkit's own documentation has, and the one the whole
    /// exercise is about: a constructor call the trimmer can see.
    /// </summary>
    [Fact]
    public void AViewModelTakingTheServiceProvider_IsBuiltDirectly()
    {
        const string source = """
            using CdCSharp.Pangea.Core.Base;
            using System;

            namespace Sample;

            public class OrderViewModel : ViewModelBase
            {
                public OrderViewModel(IServiceProvider services) : base(services) { }
            }
            """;

        string catalog = CatalogTestHelper.RunExpectingCatalog(source);

        Assert.Contains("static sp => new global::Sample.OrderViewModel(sp)", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void AViewModelTakingServices_ResolvesEachOneByType()
    {
        const string source = """
            using CdCSharp.Pangea.Core.Base;
            using System;

            namespace Sample;

            public interface IOrders;

            public class OrderViewModel : ViewModelBase
            {
                public OrderViewModel(IServiceProvider services, IOrders orders) : base(services) { }
            }
            """;

        string catalog = CatalogTestHelper.RunExpectingCatalog(source);

        Assert.Contains(
            "new global::Sample.OrderViewModel(sp, global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::Sample.IOrders>(sp))",
            catalog,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Two constructors is the container's choice to make, not the generator's: it has rules for
    /// picking one, and guessing differently here would change how the application starts.
    /// </summary>
    [Fact]
    public void AViewModelWithMoreThanOneConstructor_IsLeftToTheContainer()
    {
        const string source = """
            using CdCSharp.Pangea.Core.Base;
            using System;

            namespace Sample;

            public interface IOrders;

            public class OrderViewModel : ViewModelBase
            {
                public OrderViewModel(IServiceProvider services) : base(services) { }
                public OrderViewModel(IServiceProvider services, IOrders orders) : base(services) { }
            }
            """;

        string catalog = CatalogTestHelper.RunExpectingCatalog(source);

        Assert.Contains("ActivatorUtilities.CreateInstance(sp, typeof(global::Sample.OrderViewModel))",
            catalog, StringComparison.Ordinal);
    }

    /// <summary>A string is a configuration value, and asking a container for one fails.</summary>
    [Fact]
    public void AViewModelTakingSomethingNoContainerRegisters_IsLeftToTheContainer()
    {
        const string source = """
            using CdCSharp.Pangea.Core.Base;
            using System;

            namespace Sample;

            public class OrderViewModel : ViewModelBase
            {
                public OrderViewModel(IServiceProvider services, string title) : base(services) { }
            }
            """;

        string catalog = CatalogTestHelper.RunExpectingCatalog(source);

        Assert.Contains("ActivatorUtilities.CreateInstance", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAbstractViewModel_IsLeftOut()
    {
        const string source = """
            using CdCSharp.Pangea.Core.Base;
            using System;

            namespace Sample;

            public abstract class ScreenViewModel : ViewModelBase
            {
                protected ScreenViewModel(IServiceProvider services) : base(services) { }
            }
            """;

        Assert.Null(CatalogTestHelper.Run(source));
    }

    [Fact]
    public void AView_IsCataloguedByItsSimpleNameWithAConstructorCall()
    {
        const string source = """
            using Avalonia.Controls;

            namespace Sample.Views;

            public class OrderView : UserControl;
            """;

        string catalog = CatalogTestHelper.RunExpectingCatalog(source);

        Assert.Contains("\"OrderView\", typeof(global::Sample.Views.OrderView), static () => new global::Sample.Views.OrderView()",
            catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void ANavigationRequest_IsCataloguedWithTheScreenItNames()
    {
        const string source = """
            using CdCSharp.Pangea.Core.Abstractions;
            using CdCSharp.Pangea.Core.Base;
            using System;

            namespace Sample;

            public class OrderDetailViewModel : ViewModelBase
            {
                public OrderDetailViewModel(IServiceProvider services) : base(services) { }
            }

            public sealed record ShowOrderDetail(string Reference) : INavigationRequest<OrderDetailViewModel>;
            """;

        string catalog = CatalogTestHelper.RunExpectingCatalog(source);

        Assert.Contains("typeof(global::Sample.ShowOrderDetail), typeof(global::Sample.OrderDetailViewModel)",
            catalog, StringComparison.Ordinal);
    }

    /// <summary>
    /// An assembly with nothing to contribute gets no catalog, so nothing is loaded to say so.
    /// </summary>
    [Fact]
    public void AnAssemblyWithNothingInIt_ProducesNoCatalog()
    {
        Assert.Null(CatalogTestHelper.Run("namespace Sample; public class Ordinary;"));
    }

    [Fact]
    public void TheCatalog_IsNamespacedByTheAssemblyItDescribes()
    {
        const string source = """
            using Avalonia.Controls;

            namespace Sample.Views;

            public class OrderView : UserControl;
            """;

        string catalog = CatalogTestHelper.RunExpectingCatalog(source, "My.Shell.App");

        Assert.Contains("namespace CdCSharp.Pangea.Generated.My_Shell_App;", catalog, StringComparison.Ordinal);
        Assert.Contains("public string AssemblyName => \"My.Shell.App\";", catalog, StringComparison.Ordinal);
    }

    /// <summary>
    /// The catalog has to be registered without anything calling it, or an application would have
    /// to know it exists.
    /// </summary>
    [Fact]
    public void TheCatalog_RegistersItselfFromAModuleInitializer()
    {
        const string source = """
            using Avalonia.Controls;

            namespace Sample.Views;

            public class OrderView : UserControl;
            """;

        string catalog = CatalogTestHelper.RunExpectingCatalog(source);

        Assert.Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]", catalog, StringComparison.Ordinal);
        Assert.Contains("global::CdCSharp.Pangea.Core.Base.PangeaCatalogs.Add(new PangeaCatalog())",
            catalog, StringComparison.Ordinal);
    }
}
