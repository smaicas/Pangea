using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Tests.Int.Views;
using CdCSharp.Pangea.Theming.Controls;
using CdCSharp.Pangea.Theming.Abstractions;
using Avalonia.Threading;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace CdCSharp.Pangea.Tests.Int.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IWindowManager _windowManager;

    [Binding] private string _greeting = "🎨 Pangea UI Showcase";
    [Binding(ReadOnly = true)] private string _statusMessage = "Theme system ready ✨";
    [Binding] private int _progressValue = 75;

    public ThemeSelectorViewModel ThemeSelector { get; }

    public MainWindowViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _themeService = serviceProvider.GetRequiredService<IThemeService>();
        _windowManager = serviceProvider.GetRequiredService<IWindowManager>();
        ThemeSelector = serviceProvider.GetRequiredService<ThemeSelectorViewModel>();

        UpdateStatusMessage();
    }

    public RelayCommand UpdateStatusCommand => CreateCommand(ExecuteUpdateStatus);
    public RelayCommand OpenCommandTestCommand => CreateCommand(OpenCommandTest);
    public RelayCommand OpenControlGalleryCommand => CreateCommand(OpenControlGallery);
    public RelayCommand OpenNavigationDemoCommand => CreateCommand(OpenNavigationDemo);

    public string ProgressText => $"Demo Progress: {ProgressValue}%";

    private void ExecuteUpdateStatus()
    {
        string[] messages = new[]
        {
            "✨ Theme switching demonstration active", "🎨 All controls synchronized with theme",
            "🔄 Dynamic resource bindings working", "📱 Responsive theme adaptation complete",
            "🌟 Color palette showcase updated", "🚀 Pangea UI theme system operational"
        };

        Random random = new Random();
        _statusMessage = messages[random.Next(messages.Length)];
        OnPropertyChanged(nameof(StatusMessage));

        // Update progress with smooth animation simulation
        ProgressValue = random.Next(45, 100);
    }

    private async Task OpenCommandTest()
    {
        await _windowManager.ShowWindowAsync<CommandTestWindow, CommandTestViewModel>();
    }

    private async Task OpenNavigationDemo()
    {
        await _windowManager.ShowWindowAsync<NavigationDemoWindow, NavigationDemoViewModel>();
    }

    private void OpenControlGallery()
    {
        // A plain synchronous command body, running on the UI thread like any MVVM command should.
        // The gallery is pure XAML with no view model of its own; it only needs the shared theme
        // selector so switching theme from it drives the whole application.
        ControlGalleryWindow gallery = new();
        gallery.UseThemeSelector(ThemeSelector);
        gallery.Show();
    }

    partial void OnProgressValueChanged()
    {
        OnPropertyChanged(nameof(ProgressText));
    }

    private void UpdateStatusMessage()
    {
        bool isDark = _themeService.CurrentVariant == Avalonia.Styling.ThemeVariant.Dark;
        _statusMessage = isDark
            ? $"🌙 {_themeService.CurrentTheme} dark - Warm & minimal design"
            : $"☀️ {_themeService.CurrentTheme} light - Clean & bright design";
        OnPropertyChanged(nameof(StatusMessage));
    }
}