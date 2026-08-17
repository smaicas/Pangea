using Avalonia.Controls;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CdCSharp.Pangea.Theming.Tests.Infrastructure;

/// <summary>
/// Discovers which control types the theme actually styles, by reading the TargetType of every
/// ControlTheme in the dictionaries. Driving the smoke tests off the dictionaries (rather than a
/// hand-written list) means a control added by a later Avalonia version is covered automatically.
/// </summary>
public static class ThemedControls
{
    private static readonly Regex TargetTypePattern =
        new(@"<ControlTheme[^>]*?TargetType=""(?:\{x:Type\s+)?([A-Za-z0-9_.:]+?)\}?""",
            RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Controls that cannot be instantiated standalone in a smoke test, with the reason.
    /// Every entry is asserted to still be a real target type, so the list cannot rot silently.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> NotSmokeTestable = new Dictionary<string, string>
    {
        ["ManagedFileChooser"] = "requires a ManagedFileChooserViewModel as DataContext",
        ["NotificationCard"] = "only valid inside a WindowNotificationManager host",
        ["TextSelectionHandle"] = "positioned by TextBox internals, throws when orphaned",
    };

    public static IReadOnlyList<string> TargetTypeNames() =>
        ThemeSources.ControlDictionaries()
            .SelectMany(file => TargetTypePattern.Matches(ThemeSources.Read(file)))
            .Select(match => match.Groups[1].Value)
            .Select(name => name.Contains(':') ? name[(name.IndexOf(':') + 1)..] : name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Names that resolve to a control we can new up and drop into a window.
    /// TopLevels are excluded structurally: they are roots, not children.
    /// </summary>
    public static IReadOnlyList<string> SmokeTestableNames() =>
        TargetTypeNames()
            .Where(name => !NotSmokeTestable.ContainsKey(name))
            .Where(name => Resolve(name) is { } type && IsInstantiableChild(type))
            .ToList();

    public static Type? Resolve(string simpleName) => ControlTypes.Value.GetValueOrDefault(simpleName);

    private static bool IsInstantiableChild(Type type) =>
        !type.IsAbstract &&
        !typeof(TopLevel).IsAssignableFrom(type) &&
        type.GetConstructor(Type.EmptyTypes) is not null;

    private static readonly Lazy<Dictionary<string, Type>> ControlTypes = new(() =>
    {
        Dictionary<string, Type> types = new(StringComparer.Ordinal);

        // Walk the Avalonia assemblies the theme can possibly target.
        IEnumerable<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true);

        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in SafeGetTypes(assembly))
            {
                if (type.IsPublic && typeof(Control).IsAssignableFrom(type))
                {
                    types.TryAdd(type.Name, type);
                }
            }
        }

        return types;
    });

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
