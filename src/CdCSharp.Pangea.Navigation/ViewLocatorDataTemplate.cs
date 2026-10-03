using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CdCSharp.Pangea.Navigation.Abstractions;

namespace CdCSharp.Pangea.Navigation;

/// <summary>
/// Displays any view model the locator knows, wherever it appears as content.
/// </summary>
/// <remarks>
/// <para>
/// Without it the naming convention only applied inside a <see cref="NavigationHost"/>. A
/// <c>ContentControl</c> bound to a sub-panel's view model - <c>Content="{Binding Details}"</c> -
/// fell through to Avalonia's default and painted the type name, and every screen composed of
/// panels had to name its controls by hand.
/// </para>
/// <para>
/// Installed at the end of the application's templates, so a template the application declares
/// for the same type still wins, as does any template closer to the control.
/// </para>
/// </remarks>
public sealed class ViewLocatorDataTemplate : IDataTemplate
{
    private readonly IViewLocator _locator;

    public ViewLocatorDataTemplate(IViewLocator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        _locator = locator;
    }

    /// <summary>
    /// Asked for every piece of content without a closer template, so it has to be cheap and can
    /// never throw: a type with no view is not a match, and Avalonia moves on to the next template.
    /// </summary>
    public bool Match(object? data) => data is not null && _locator.CanLocate(data.GetType());

    public Control? Build(object? param) => param is null ? null : _locator.Locate(param);

    /// <summary>
    /// Adds the template to <paramref name="application"/>, replacing one installed earlier.
    /// </summary>
    /// <remarks>
    /// Replaced rather than added again: an application built twice in one process - tests do this -
    /// would otherwise keep answering with the first container's locator.
    /// </remarks>
    public static void Install(Application application, IViewLocator locator)
    {
        ArgumentNullException.ThrowIfNull(application);

        for (int i = application.DataTemplates.Count - 1; i >= 0; i--)
        {
            if (application.DataTemplates[i] is ViewLocatorDataTemplate) application.DataTemplates.RemoveAt(i);
        }

        application.DataTemplates.Add(new ViewLocatorDataTemplate(locator));
    }
}
