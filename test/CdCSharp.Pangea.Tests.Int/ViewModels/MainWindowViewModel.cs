using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Theming.Controls;
using CdCSharp.Pangea.Theming.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CdCSharp.Pangea.Tests.Int.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;

    [Binding] private string _greeting = "🎨 Pangea UI Showcase";
    [Binding(ReadOnly = true)] private string _statusMessage = "Theme system ready ✨";
    [Binding] private int _progressValue = 75;

    public ThemeSelectorViewModel ThemeSelector { get; }

    public MainWindowViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _themeService = serviceProvider.GetRequiredService<IThemeService>();
        ThemeSelector = serviceProvider.GetRequiredService<ThemeSelectorViewModel>();
        
        UpdateStatusMessage();
    }

    public RelayCommand UpdateStatusCommand => CreateCommand(ExecuteUpdateStatus);

    public string ProgressText => $"Demo Progress: {ProgressValue}%";

    private void ExecuteUpdateStatus()
    {
        string[] messages = new[]
        {
            "✨ Theme switching demonstration active",
            "🎨 All controls synchronized with theme",
            "🔄 Dynamic resource bindings working",
            "📱 Responsive theme adaptation complete",
            "🌟 Color palette showcase updated",
            "🚀 Pangea UI theme system operational"
        };

        Random random = new Random();
        _statusMessage = messages[random.Next(messages.Length)];
        OnPropertyChanged(nameof(StatusMessage));

        // Update progress with smooth animation simulation
        ProgressValue = random.Next(45, 100);
    }

    partial void OnProgressValueChanged()
    {
        OnPropertyChanged(nameof(ProgressText));
    }

    private void UpdateStatusMessage()
    {
        string currentTheme = _themeService.GetCurrentTheme() ?? "Unknown";
        _statusMessage = currentTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
            ? "🌙 Dark theme active - Warm & minimal design"
            : "☀️ Light theme active - Clean & bright design";
        OnPropertyChanged(nameof(StatusMessage));
    }
}