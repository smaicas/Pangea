namespace CdCSharp.Pangea.Localization;

public class LocalizationOptions
{
    public static LocalizationOptions Default => new()
    {
        DefaultCulture = "en-US",
        SupportedCultures = new List<string> { "en-US", "es-ES" },
        AutoDetectCulture = true,
        ResourceAssemblyNames = new List<string>()
    };

    public string DefaultCulture { get; set; } = "en-US";
    public List<string> SupportedCultures { get; set; } = new() { "en-US", "es-ES" };
    public bool AutoDetectCulture { get; set; } = true;
    public List<string> ResourceAssemblyNames { get; set; } = new();
}