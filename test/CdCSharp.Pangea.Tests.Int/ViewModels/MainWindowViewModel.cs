using CdCSharp.Pangea.Core.Base;
using CdCSharp.Pangea.Binding.Attributes;
using CdCSharp.Pangea.Tests.Int.Views;
using CdCSharp.Pangea.Theming.Controls;
using CdCSharp.Pangea.Theming.Abstractions;
using Avalonia.Threading;
using CdCSharp.Pangea.Dialogs;
using CdCSharp.Pangea.Windows;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace CdCSharp.Pangea.Tests.Int.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly IWindowManager _windowManager;
    private readonly IDialogService _dialogs;

    [Binding] private string _greeting = "🎨 Pangea UI Showcase";
    [Binding(ReadOnly = true)] private string _statusMessage = "Theme system ready ✨";
    [Binding] private int _progressValue = 75;
    [Binding(ReadOnly = true)] private string _lastDialogResult = "Nothing asked yet.";

    public ThemeSelectorViewModel ThemeSelector { get; }

    public MainWindowViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _themeService = serviceProvider.GetRequiredService<IThemeService>();
        _windowManager = serviceProvider.GetRequiredService<IWindowManager>();
        _dialogs = serviceProvider.GetRequiredService<IDialogService>();
        ThemeSelector = serviceProvider.GetRequiredService<ThemeSelectorViewModel>();

        UpdateStatusMessage();
    }

    public RelayCommand UpdateStatusCommand => CreateCommand(ExecuteUpdateStatus);
    public RelayCommand OpenCommandTestCommand => CreateCommand(OpenCommandTest);
    public RelayCommand OpenControlGalleryCommand => CreateCommand(OpenControlGallery);
    public RelayCommand OpenNavigationDemoCommand => CreateCommand(OpenNavigationDemo);
    public RelayCommand OpenValidationDemoCommand => CreateCommand(OpenValidationDemo);
    public RelayCommand AskToDeleteCommand => CreateCommand(AskToDelete);
    public RelayCommand AnnounceCommand => CreateCommand(Announce);

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

    private async Task OpenValidationDemo()
    {
        await _windowManager.ShowWindowAsync<ValidationDemoWindow, ValidationDemoViewModel>();
    }

    /// <summary>No window written for this: the toolkit builds it and it takes the theme.</summary>
    private async Task AskToDelete()
    {
        bool confirmed = await _dialogs.ConfirmAsync(
            "Delete order",
            "This cannot be undone. Delete the order?",
            "Delete it",
            "Keep it");

        // The returned value, shown as it came back: cancelling, pressing Escape and closing the
        // window are all the same answer, and that is easiest to believe by watching it.
        ReportDialogResult($"ConfirmAsync returned {confirmed} — " +
                           (confirmed ? "the order would be deleted." : "nothing was deleted."));
    }

    private async Task Announce()
    {
        await _dialogs.AlertAsync("Saved", "Your changes have been saved.");

        ReportDialogResult("AlertAsync returned — it has nothing to report but that you saw it.");
    }

    private void ReportDialogResult(string message)
    {
        _lastDialogResult = $"{DateTime.Now:HH:mm:ss}  {message}";
        OnPropertyChanged(nameof(LastDialogResult));
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