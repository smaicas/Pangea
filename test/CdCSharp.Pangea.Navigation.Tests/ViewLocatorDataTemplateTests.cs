using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Navigation.Tests.Infrastructure;
using System.Reflection;

namespace CdCSharp.Pangea.Navigation.Tests;

/// <summary>
/// The naming convention outside the navigation host: a view model placed in any content control.
/// </summary>
/// <remarks>
/// Before this a <c>ContentControl</c> bound to a sub-panel's view model painted
/// "App.ViewModels.DetailsViewModel", and composing a screen out of panels meant naming each view
/// by hand.
/// </remarks>
public class ViewLocatorDataTemplateTests
{
    private static ViewLocator CreateLocator()
    {
        TypeRegistry registry = new([Assembly.GetExecutingAssembly()]);
        registry.Initialize();

        return new ViewLocator(new StubServices(), registry);
    }

    [Fact]
    public void CanLocate_IsTrueForAViewModelWithAView() =>
        Assert.True(CreateLocator().CanLocate(typeof(OrderViewModel)));

    [Fact]
    public void CanLocate_IsFalseRatherThanThrowing()
    {
        ViewLocator locator = CreateLocator();

        Assert.False(locator.CanLocate(typeof(HomelessViewModel)));
        Assert.False(locator.CanLocate(typeof(string)));
    }

    [Fact]
    public void CanLocate_FollowsARegistrationMadeAfterAMiss()
    {
        ViewLocator locator = CreateLocator();
        Assert.False(locator.CanLocate(typeof(HomelessViewModel)));

        locator.Register<HomelessViewModel, ReportView>();

        Assert.True(locator.CanLocate(typeof(HomelessViewModel)));
    }

    [Fact]
    public void TheTemplate_LeavesOrdinaryContentAlone()
    {
        ViewLocatorDataTemplate template = new(CreateLocator());

        Assert.False(template.Match("Save"));
        Assert.False(template.Match(42));
        Assert.False(template.Match(null));
        Assert.True(template.Match(new OrderViewModel()));
    }

    [AvaloniaFact]
    public void AViewModelInAContentControl_IsDisplayedByItsView()
    {
        Application application = Application.Current!;
        ViewLocatorDataTemplate.Install(application, CreateLocator());

        try
        {
            OrderViewModel viewModel = new();
            ContentControl panel = new() { Content = viewModel };
            Window window = new() { Content = panel };
            window.Show();

            OrderView view = Assert.IsType<OrderView>(panel.Presenter?.Child);
            Assert.Same(viewModel, view.DataContext);

            window.Close();
        }
        finally
        {
            Uninstall(application);
        }
    }

    [AvaloniaFact]
    public void AViewModelWithNoView_FallsThroughInsteadOfThrowing()
    {
        Application application = Application.Current!;
        ViewLocatorDataTemplate.Install(application, CreateLocator());

        try
        {
            ContentControl panel = new() { Content = new HomelessViewModel() };
            Window window = new() { Content = panel };
            window.Show();

            Assert.IsNotType<ReportView>(panel.Presenter?.Child);
            window.Close();
        }
        finally
        {
            Uninstall(application);
        }
    }

    [AvaloniaFact]
    public void InstallingAgain_ReplacesTheEarlierTemplate()
    {
        Application application = Application.Current!;

        try
        {
            ViewLocatorDataTemplate.Install(application, CreateLocator());
            ViewLocatorDataTemplate.Install(application, CreateLocator());

            Assert.Single(application.DataTemplates.OfType<ViewLocatorDataTemplate>());
        }
        finally
        {
            Uninstall(application);
        }
    }

    private static void Uninstall(Application application)
    {
        foreach (IDataTemplate template in application.DataTemplates.OfType<ViewLocatorDataTemplate>().ToList())
        {
            application.DataTemplates.Remove(template);
        }
    }
}
