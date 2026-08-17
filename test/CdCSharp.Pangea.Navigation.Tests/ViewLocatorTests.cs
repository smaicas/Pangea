using Avalonia.Controls;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Navigation.Tests.Infrastructure;
using System.Reflection;

namespace CdCSharp.Pangea.Navigation.Tests;

/// <summary>
/// Finding the view for a view model by name. Both conventions in common use have to work, or the
/// rule is a coin toss for whoever names the file.
/// </summary>
public class ViewLocatorTests
{
    private static ViewLocator Create()
    {
        // The fixtures live in this assembly, so it is named explicitly rather than left to the
        // framework heuristics.
        TypeRegistry registry = new([Assembly.GetExecutingAssembly()]);
        registry.Initialize();

        return new ViewLocator(new StubServices(), registry);
    }

    [Fact]
    public void OrderViewModel_IsDisplayedByOrderView()
    {
        Control view = Create().Locate(new OrderViewModel());

        Assert.IsType<OrderView>(view);
    }

    [Fact]
    public void TheViewModelBecomesTheDataContext()
    {
        OrderViewModel viewModel = new();

        Control view = Create().Locate(viewModel);

        Assert.Same(viewModel, view.DataContext);
    }

    [Fact]
    public void AnExplicitRegistrationWinsOverTheConvention()
    {
        ViewLocator locator = Create();
        locator.Register<OrderViewModel, ReportView>();

        Assert.IsType<ReportView>(locator.Locate(new OrderViewModel()));
    }

    [Fact]
    public void AViewModelWithNoView_SaysWhatToNameIt()
    {
        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => Create().Locate(new HomelessViewModel()));

        Assert.Contains("HomelessView", error.Message, StringComparison.Ordinal);
        Assert.Contains("Homeless'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ATypeThatIsNotAViewModel_SaysSo()
    {
        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => Create().Locate(new PlainViewModel()));

        // PlainViewModel ends in ViewModel, so the failure is the missing view, not the name.
        Assert.Contains("PlainView", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LocatingNull_IsRejected() =>
        Assert.Throws<ArgumentNullException>(() => Create().Locate(null!));
}

public class HomelessViewModel;
