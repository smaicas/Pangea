using CdCSharp.Pangea.Core.Abstractions;
using CdCSharp.Pangea.Localization.Abstractions;
using System.ComponentModel;

namespace CdCSharp.Pangea.Localization;

/// <summary>
/// The application's strings, indexed by resource key, for XAML to bind to:
/// <c>Text="{Binding Strings[Home_Title]}"</c>.
/// </summary>
/// <remarks>
/// <para>
/// One object shared by every view model, registered by <see cref="LocalizationFeature"/>. It is
/// what makes changing the language at runtime visible: a change of culture re-reads every string
/// on screen at once, because every one of them is a binding through this indexer.
/// </para>
/// <para>
/// <c>"Item[]"</c> is the name a binding listens for when what changed is an indexer. The
/// alternative is a property per key, and then adding a string means editing a view model too.
/// </para>
/// <para>
/// What this does <em>not</em> refresh is anything the culture affects without going through it -
/// a <c>StringFormat</c>, a date, a number. Those are formatted by the binding itself, and a
/// binding that has not been told anything changed will not run again.
/// </para>
/// </remarks>
public sealed class LocalizedStrings : INotifyPropertyChanged, IDisposable
{
    /// <summary>The property name that means "every indexed value", to a binding.</summary>
    private const string IndexerName = "Item[]";

    private readonly ILocalizationService _localization;
    private readonly IUIDispatcher? _dispatcher;

    /// <param name="localization">The service whose current culture the strings are read in.</param>
    /// <param name="dispatcher">
    /// Used to raise the change on the UI thread. Optional: an application hosting the localization
    /// feature without the Pangea application model has no dispatcher, and nothing bound to notify.
    /// </param>
    public LocalizedStrings(ILocalizationService localization, IUIDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(localization);

        _localization = localization;
        _dispatcher = dispatcher;
        _localization.CultureChanged += OnCultureChanged;
    }

    /// <summary>The string for <paramref name="key"/> in the current culture, or the key itself.</summary>
    /// <remarks>
    /// <c>[LocalizationKey]</c> is what has the analyzer check every key written at a call site
    /// against the project's .resx files.
    /// </remarks>
    public string this[[LocalizationKey] string key] => _localization.GetString(key);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose() => _localization.CultureChanged -= OnCultureChanged;

    /// <summary>
    /// Announces that every string has changed, on the UI thread.
    /// </summary>
    /// <remarks>
    /// The culture can be set from anywhere - a settings screen, a startup feature reading a file -
    /// and a binding updated off the UI thread is an exception the application never sees coming.
    /// </remarks>
    private void OnCultureChanged(object? sender, CultureChangedEventArgs e)
    {
        if (_dispatcher is null || _dispatcher.CheckAccess())
        {
            RaiseEverythingChanged();
            return;
        }

        _dispatcher.Post(RaiseEverythingChanged);
    }

    private void RaiseEverythingChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(IndexerName));
}
