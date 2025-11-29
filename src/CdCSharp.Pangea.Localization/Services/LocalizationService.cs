using CdCSharp.Pangea.Localization.Abstractions;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Resources;
using System.Reflection;

namespace CdCSharp.Pangea.Localization.Services;

public class LocalizationService : ILocalizationService
{
    private readonly IOptions<LocalizationOptions> _options;
    private readonly List<ResourceManager> _resourceManagers = new();

    public LocalizationService(IOptions<LocalizationOptions> options)
    {
        _options = options;
        
        InitializeResourceManagers();
        InitializeCulture();
    }

    public CultureInfo CurrentCulture { get; private set; }

    public IEnumerable<CultureInfo> SupportedCultures => 
        _options.Value.SupportedCultures.Select(c => new CultureInfo(c));

    public event EventHandler<CultureChangedEventArgs>? CultureChanged;

    public string GetString(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        foreach (ResourceManager resourceManager in _resourceManagers)
        {
            try
            {
                string? value = resourceManager.GetString(key, CurrentCulture);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
            catch
            {
                // Continue to next resource manager
            }
        }

        return key; // Return key as fallback
    }

    public void SetCulture(string cultureName)
    {
        LocalizationOptions opts = _options.Value;
        
        if (!opts.SupportedCultures.Contains(cultureName))
        {
            throw new NotSupportedException($"Culture '{cultureName}' is not supported. Supported cultures: {string.Join(", ", opts.SupportedCultures)}");
        }

        CultureInfo oldCulture = CurrentCulture;
        CurrentCulture = new CultureInfo(cultureName);
        CultureInfo.CurrentCulture = CurrentCulture;
        CultureInfo.CurrentUICulture = CurrentCulture;

        CultureChanged?.Invoke(this, new CultureChangedEventArgs(oldCulture, CurrentCulture));
    }

    private void InitializeResourceManagers()
    {
        LocalizationOptions opts = _options.Value;
        
        foreach (string assemblyName in opts.ResourceAssemblyNames)
        {
            try
            {
                Assembly? assembly = Assembly.LoadFrom(assemblyName);
                if (assembly != null)
                {
                    Type[] types = assembly.GetTypes();
                    foreach (Type type in types)
                    {
                        if (type.Name.EndsWith("Resources") && type.GetProperty("ResourceManager") != null)
                        {
                            PropertyInfo? property = type.GetProperty("ResourceManager", BindingFlags.Public | BindingFlags.Static);
                            if (property?.GetValue(null) is ResourceManager resourceManager)
                            {
                                _resourceManagers.Add(resourceManager);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore failed assembly loads
            }
        }
    }

    private void InitializeCulture()
    {
        LocalizationOptions opts = _options.Value;
        CurrentCulture = new CultureInfo(opts.DefaultCulture);
        
        if (opts.AutoDetectCulture)
        {
            DetectSystemCulture();
        }
        
        CultureInfo.CurrentCulture = CurrentCulture;
        CultureInfo.CurrentUICulture = CurrentCulture;
    }

    private void DetectSystemCulture()
    {
        LocalizationOptions opts = _options.Value;
        string systemCulture = CultureInfo.CurrentUICulture.Name;
        
        if (opts.SupportedCultures.Contains(systemCulture))
        {
            CurrentCulture = new CultureInfo(systemCulture);
        }
    }
}