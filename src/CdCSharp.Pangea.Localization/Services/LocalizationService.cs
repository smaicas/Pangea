using CdCSharp.Pangea.Localization.Abstractions;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace CdCSharp.Pangea.Localization.Services;

/// <summary>
/// Reads localized strings from the resource assemblies declared in <see cref="LocalizationOptions"/>
/// and owns the application's current culture.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly LocalizationOptions _options;
    private readonly IReadOnlyList<ResourceManager> _resourceManagers;
    private readonly IReadOnlyList<CultureInfo> _supportedCultures;

    public LocalizationService(IOptions<LocalizationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _resourceManagers = DiscoverResourceManagers(_options.ResourceAssemblies);
        _supportedCultures = _options.SupportedCultures.Select(CultureInfo.GetCultureInfo).ToList();

        CurrentCulture = ResolveInitialCulture();
        ApplyCulture(CurrentCulture);
    }

    public CultureInfo CurrentCulture { get; private set; }

    public IEnumerable<CultureInfo> SupportedCultures => _supportedCultures;

    public event EventHandler<CultureChangedEventArgs>? CultureChanged;

    /// <summary>
    /// Resolves <paramref name="key"/> against the resource assemblies in order. An unresolved key
    /// is returned as-is, which keeps a missing translation visible in the UI instead of blank.
    /// </summary>
    public string GetString(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;

        foreach (ResourceManager resourceManager in _resourceManagers)
        {
            string? value;

            try
            {
                value = resourceManager.GetString(key, CurrentCulture);
            }
            catch (MissingManifestResourceException)
            {
                // This assembly ships no resources for the current culture; the next one may.
                continue;
            }

            if (!string.IsNullOrEmpty(value)) return value;
        }

        return key;
    }

    public void SetCulture(string cultureName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);

        if (!_options.SupportedCultures.Contains(cultureName, StringComparer.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Culture '{cultureName}' is not supported. Supported cultures: {string.Join(", ", _options.SupportedCultures)}");
        }

        CultureInfo previous = CurrentCulture;
        CultureInfo next = CultureInfo.GetCultureInfo(cultureName);

        if (string.Equals(previous.Name, next.Name, StringComparison.OrdinalIgnoreCase)) return;

        CurrentCulture = next;
        ApplyCulture(next);

        CultureChanged?.Invoke(this, new CultureChangedEventArgs(previous, next));
    }

    /// <summary>
    /// Applies the culture to the whole application, not just whichever thread asked for the change:
    /// <see cref="CultureInfo.DefaultThreadCurrentCulture"/> covers threads started later, and the
    /// calling thread needs setting explicitly.
    /// </summary>
    private static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>
    /// The culture to start in: what the device asks for, when the application offers it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Matched on the language when the exact name is not offered. A phone reports its locale in
    /// whatever form it likes - Android commonly gives a bare "en" or "es", iOS gives "en-GB" for a
    /// British device - and an application offering "en-US" wants all of them. Comparing the full
    /// name only meant auto-detection silently never fired on a device, leaving every user on the
    /// default language however their phone was set.
    /// </para>
    /// <para>
    /// The region still comes from what the application offers, because that is the resource file
    /// it actually has. A British device gets en-US strings, which is the right trade against no
    /// English at all.
    /// </para>
    /// </remarks>
    private CultureInfo ResolveInitialCulture()
    {
        if (_options.AutoDetectCulture && Match(CultureInfo.CurrentUICulture) is { } detected) return detected;

        return CultureInfo.GetCultureInfo(_options.DefaultCulture);
    }

    /// <summary>The supported culture closest to <paramref name="wanted"/>, or null.</summary>
    private CultureInfo? Match(CultureInfo wanted)
    {
        foreach (string supported in _options.SupportedCultures)
        {
            if (string.Equals(supported, wanted.Name, StringComparison.OrdinalIgnoreCase))
            {
                return CultureInfo.GetCultureInfo(supported);
            }
        }

        string language = wanted.TwoLetterISOLanguageName;

        foreach (string supported in _options.SupportedCultures)
        {
            if (string.Equals(
                    CultureInfo.GetCultureInfo(supported).TwoLetterISOLanguageName,
                    language,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CultureInfo.GetCultureInfo(supported);
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the generated resource classes in the given assemblies: a type exposing a public
    /// static <see cref="ResourceManager"/> property, which is what the .resx designer emits.
    /// </summary>
    private static List<ResourceManager> DiscoverResourceManagers(IEnumerable<Assembly> assemblies)
    {
        List<ResourceManager> managers = [];

        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in SafeGetTypes(assembly))
            {
                PropertyInfo? property = type.GetProperty(
                    nameof(ResourceManager), BindingFlags.Public | BindingFlags.Static);

                if (property?.PropertyType == typeof(ResourceManager) &&
                    property.GetValue(null) is ResourceManager manager)
                {
                    managers.Add(manager);
                }
            }
        }

        return managers;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }
}
