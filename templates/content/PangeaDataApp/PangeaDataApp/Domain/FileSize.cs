namespace PangeaDataApp.Domain;

/// <summary>A size in bytes, as a person would say it.</summary>
public static class FileSize
{
    private const long Kilobyte = 1024;
    private const long Megabyte = Kilobyte * 1024;
    private const long Gigabyte = Megabyte * 1024;

    /// <summary>
    /// Describes <paramref name="bytes"/>, or says there is no file.
    /// </summary>
    /// <remarks>
    /// Scaled rather than always in kilobytes. A database that has been used for a year is tens of
    /// megabytes, and "38400 KB" is a number somebody has to divide in their head before it means
    /// anything.
    /// </remarks>
    public static string Describe(long? bytes) => bytes switch
    {
        null => "no file",
        < 0 => "no file",
        < Kilobyte => $"{bytes} B",
        < Megabyte => $"{bytes / (double)Kilobyte:0.#} KB",
        < Gigabyte => $"{bytes / (double)Megabyte:0.#} MB",
        _ => $"{bytes / (double)Gigabyte:0.##} GB"
    };
}
