namespace CdCSharp.Pangea.Storage;

public class StorageOptions
{
    public static StorageOptions Default => new()
    {
        ApplicationName = "PangeaApp",
        UsePortableMode = false,
        CustomDataPath = null
    };

    public string ApplicationName { get; set; } = "PangeaApp";
    public bool UsePortableMode { get; set; } = false;
    public string? CustomDataPath { get; set; }
}