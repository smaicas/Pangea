# CdCSharp.Pangea

Modern Avalonia Toolkit with MVVM, automatic property binding, theming, and cross-platform storage.

## Requirements

- .NET 9.0
- Avalonia 11.3.2+

## Installation

```bash~~~~
dotnet add package CdCSharp.Pangea
```

## Features

### Core Architecture
- Modular feature-based system with automatic discovery
- Dependency injection integration with Microsoft.Extensions
- Type registry for automatic window/viewmodel resolution
- Command factory with error handling

### Binding System
- Source generators for automatic property creation
- `[Binding]` attribute on fields generates properties with `INotifyPropertyChanged`
- Dependency tracking for computed properties
- Command invalidation on property changes
- Collection modification detection

### MVVM Infrastructure
- `PangeaViewModelBase` with built-in binding support
- `RelayCommand` and `RelayCommand<T>` implementations
- Navigation services with parameter passing
- Window management services

### Theming Engine
- Multi-theme support (Light/Dark/Custom)
- Platform-aware theme detection
- Runtime theme switching
- Resource-based theme definitions
- Source generators for theme resource management

### Storage System
- Cross-platform path providers (Windows/Linux/macOS/Portable)
- Configurable data storage locations
- File system abstraction layer
- Support for portable applications

### Localization
- Resource-based localization system
- Runtime language switching
- Binding-aware localization updates

## Basic Usage

### Application Setup

```csharp
public partial class App : PangeaApplication
{
    protected override PangeaOptions GetPangeaOptions()
    {
        return new PangeaOptions
        {
            Window = 
            {
                AutoDiscoverMainWindow = true,
                MainWindowType = typeof(MainWindow),
                MainViewModelType = typeof(MainViewModel)
            }
        };
    }
}

// Program.cs
public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace()
        .UsePangea(); // Enables Pangea framework
```

### ViewModel with Automatic Binding

```csharp
public partial class MainViewModel : PangeaViewModelBase
{
    [Binding] private string _title = "Hello Pangea";
    [Binding] private int _counter;
    [Binding] private bool _isEnabled = true;
    
    // Source generator creates:
    // public string Title { get => _title; set => SetProperty(ref _title, value); }
    // public int Counter { get => _counter; set => SetProperty(ref _counter, value); }
    // public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    
    public RelayCommand IncrementCommand => CreateCommand(Increment);
    
    private void Increment()
    {
        Counter++;
    }
}
```

### Theme Management

```csharp
public class ThemeViewModel : PangeaViewModelBase
{
    private readonly IThemeService _themeService;
    
    public ThemeViewModel(IThemeService themeService)
    {
        _themeService = themeService;
    }
    
    public RelayCommand SwitchToLightCommand => CreateCommand(() => 
        _themeService.SetThemeVariant(PangeaThemeVariant.Light));
        
    public RelayCommand SwitchToDarkCommand => CreateCommand(() => 
        _themeService.SetThemeVariant(PangeaThemeVariant.Dark));
}
```

### Storage Usage

```csharp
public class DataService
{
    private readonly IStorageService _storage;
    
    public DataService(IStorageService storage)
    {
        _storage = storage;
    }
    
    public async Task SaveConfigAsync(Config config)
    {
        string path = _storage.GetDataFilePath("config.json");
        await _storage.WriteTextAsync(path, JsonSerializer.Serialize(config));
    }
    
    public async Task<Config?> LoadConfigAsync()
    {
        string path = _storage.GetDataFilePath("config.json");
        if (_storage.FileExists(path))
        {
            string json = await _storage.ReadTextAsync(path);
            return JsonSerializer.Deserialize<Config>(json);
        }
        return null;
    }
}
```

## Configuration

### Storage Configuration

```csharp
services.Configure<StorageOptions>(options =>
{
    options.ApplicationName = "MyApp";
    options.UsePortableMode = false; // Use system directories
    options.CustomDataPath = @"C:\MyAppData"; // Optional custom path
});
```

### DI Configuration

```csharp
public override void ConfigureServices(IServiceCollection services)
{
    services.Configure<PangeaOptions>(options =>
    {
        options.DI.AutoRegisterViewModels = true;
        options.DI.ViewModelLifetime = ServiceLifetime.Transient;
    });
}
```

## Build From Source

```bash
git clone https://github.com/smaicas/CdCSharp.Pangea.git
cd CdCSharp.Pangea
dotnet restore
dotnet build
```

## Architecture

```
CdCSharp.Pangea/
├── Core/                    # Base abstractions and services
├── Binding/                 # Automatic property binding
├── Binding.CodeGeneration/  # Source generators for binding
├── Theming/                 # Theme management system  
├── Theming.CodeGeneration/  # Source generators for theming
├── Storage/                 # Cross-platform file storage
└── Localization/           # Multi-language support
```

## License

MIT