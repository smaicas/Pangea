using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Theming.Abstractions;
using CdCSharp.Pangea.Theming.Controls;
using CdCSharp.Pangea.Theming.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CdCSharp.Pangea.Theming.Tests;

/// <summary>
/// The two ways the selector is handed a view model, both exercised the way a window does it.
/// </summary>
/// <remarks>
/// The documented usage is <c>ViewModel="{Binding ThemeSelector}"</c>, which resolves against the
/// DataContext the control inherits from its host. That is exactly what the control must not
/// overwrite: assigning its own DataContext from ViewModel leaves the binding reading from an
/// object with no such property, and the toggle ends up with nothing behind it - no icon, and
/// nothing happens when it is pressed. Setting the property directly, as the other tests here do,
/// never notices.
/// </remarks>
public class ThemeSelectorHostingTests
{
    private sealed class Services : IServiceProvider
    {
        public ThemeService Themes { get; } = new();

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IRelayCommandFactory)) return new RelayCommandFactory(null);
            if (serviceType == typeof(IThemeService)) return Themes;

            if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(ILogger<>))
            {
                return Activator.CreateInstance(
                    typeof(NullLogger<>).MakeGenericType(serviceType.GetGenericArguments()[0]));
            }

            return null;
        }
    }

    /// <summary>Stands in for the window's view model, which is what the binding reads.</summary>
    private sealed class Host
    {
        public Host(ThemeSelectorViewModel selector) => ThemeSelector = selector;

        public ThemeSelectorViewModel ThemeSelector { get; }
    }

    [AvaloniaFact]
    public void BoundToAHostsViewModel_TheSelectorKeepsIt()
    {
        Services services = new();
        ThemeSelectorViewModel viewModel = new(services);

        // <theming:ThemeSelector ViewModel="{Binding ThemeSelector}" />, spelled out.
        ThemeSelector selector = new();
        selector.Bind(ThemeSelector.ViewModelProperty, new Avalonia.Data.Binding(nameof(Host.ThemeSelector)));

        Window window = new() { DataContext = new Host(viewModel), Content = selector, Width = 200, Height = 200 };
        window.Show();

        Assert.Same(viewModel, selector.ViewModel);

        ToggleButton toggle = selector.GetVisualDescendants().OfType<ToggleButton>().Single();
        TextBlock icon = toggle.GetVisualDescendants().OfType<TextBlock>().Single();

        Assert.False(string.IsNullOrWhiteSpace(icon.Text), "The toggle shows no icon.");

        Avalonia.Styling.ThemeVariant before = services.Themes.CurrentVariant;
        toggle.IsChecked = toggle.IsChecked != true;

        Assert.NotEqual(before, services.Themes.CurrentVariant);
    }

    /// <summary>Handing it a DataContext is the other way in, and still has to work.</summary>
    [AvaloniaFact]
    public void GivenAsADataContext_TheSelectorAdoptsIt()
    {
        ThemeSelectorViewModel viewModel = new(new Services());
        ThemeSelector selector = new() { DataContext = viewModel };

        Window window = new() { Content = selector, Width = 200, Height = 200 };
        window.Show();

        Assert.Same(viewModel, selector.ViewModel);

        TextBlock icon = selector.GetVisualDescendants().OfType<TextBlock>().Single();

        Assert.False(string.IsNullOrWhiteSpace(icon.Text), "The toggle shows no icon.");
    }
}
