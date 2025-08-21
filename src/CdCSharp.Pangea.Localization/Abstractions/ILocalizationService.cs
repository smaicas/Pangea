namespace CdCSharp.Pangea.Localization.Abstractions;

using System.Globalization;
public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }
    IEnumerable<CultureInfo> SupportedCultures { get; }
    
    string GetString(string key);
    void SetCulture(string cultureName);
    
    event EventHandler<CultureChangedEventArgs>? CultureChanged;
}

public class CultureChangedEventArgs : EventArgs
{
    public CultureInfo PreviousCulture { get; }
    public CultureInfo CurrentCulture { get; }

    public CultureChangedEventArgs(CultureInfo previousCulture, CultureInfo currentCulture)
    {
        PreviousCulture = previousCulture;
        CurrentCulture = currentCulture;
    }
}