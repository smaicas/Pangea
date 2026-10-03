using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CdCSharp.Pangea.Core.Base;

/// <summary>
/// Change notification and nothing else: the base for a model the UI edits in place.
/// </summary>
/// <remarks>
/// <para>
/// A settings object, a step in a document, a row being edited - types that are serialized,
/// cloned and compared, and are not screens. They still have to notify, because a view is bound to
/// them, and <c>[Binding]</c> works on them exactly as it does on a view model: generated
/// properties, computed properties notified from what they read, change hooks.
/// </para>
/// <para>
/// No constructor arguments, no container, no commands, no validation, no lifecycle. Those belong
/// to <see cref="ViewModelBase"/>, which derives from this. A model that needs any of them is a
/// view model.
/// </para>
/// </remarks>
public abstract class ObservableModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
