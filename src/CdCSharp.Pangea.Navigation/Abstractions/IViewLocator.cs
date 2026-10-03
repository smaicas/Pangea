using Avalonia.Controls;

namespace CdCSharp.Pangea.Navigation.Abstractions;

/// <summary>Finds the view that displays a view model.</summary>
public interface IViewLocator
{
    /// <summary>Binds <typeparamref name="TView"/> to <typeparamref name="TViewModel"/>, ahead of the naming convention.</summary>
    void Register<TViewModel, TView>()
        where TViewModel : class
        where TView : Control;

    /// <summary>
    /// Whether <see cref="Locate"/> would find a view for <paramref name="viewModelType"/>. Never
    /// throws: a type with no view, or a name the convention cannot read, is simply false.
    /// </summary>
    bool CanLocate(Type viewModelType);

    /// <summary>Builds the view for <paramref name="viewModel"/>, with it as the data context.</summary>
    /// <exception cref="InvalidOperationException">No view is registered for the type and none matches the convention.</exception>
    Control Locate(object viewModel);
}
