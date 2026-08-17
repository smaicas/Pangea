using System.Reflection;

namespace CdCSharp.Pangea.Localization;

public class LocalizationOptions
{
    public static LocalizationOptions Default => new();

    /// <summary>Culture used when the system culture is unknown or auto-detection is off.</summary>
    public string DefaultCulture { get; set; } = "en-US";

    public List<string> SupportedCultures { get; set; } = ["en-US", "es-ES"];

    /// <summary>Start on the system culture when it is one of the supported ones.</summary>
    public bool AutoDetectCulture { get; set; } = true;

    /// <summary>
    /// Assemblies holding the resource classes to read strings from, in priority order.
    /// </summary>
    public List<Assembly> ResourceAssemblies { get; } = [];
}
