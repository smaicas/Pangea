using CdCSharp.Pangea.Core.Abstractions;

namespace CdCSharp.Pangea.Core.Base;

/// <summary>
/// Where the generated catalogs collect, one per assembly.
/// </summary>
/// <remarks>
/// <para>
/// Static, because a catalog describes an assembly rather than an application: it is the same
/// however many times a process starts one, and the module initializer that registers it runs once
/// when the assembly is first touched.
/// </para>
/// <para>
/// Registering the same catalog twice is a no-op, so an assembly loaded through more than one path
/// contributes its types once.
/// </para>
/// </remarks>
public static class PangeaCatalogs
{
    private static readonly object Gate = new();
    private static readonly HashSet<Type> Registered = [];
    private static readonly List<IPangeaCatalog> Catalogs = [];

    /// <summary>Every catalog registered so far, in registration order.</summary>
    public static IReadOnlyList<IPangeaCatalog> All
    {
        get
        {
            lock (Gate)
            {
                return Catalogs.ToArray();
            }
        }
    }

    /// <summary>Whether anything has been generated for this application at all.</summary>
    /// <remarks>
    /// False in a project the generator never ran in - one referencing the assemblies directly, or
    /// built by something other than the SDK - and startup falls back to scanning.
    /// </remarks>
    public static bool Any
    {
        get
        {
            lock (Gate)
            {
                return Catalogs.Count > 0;
            }
        }
    }

    /// <summary>Registers <paramref name="catalog"/>. Called from generated code.</summary>
    public static void Add(IPangeaCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        lock (Gate)
        {
            if (Registered.Add(catalog.GetType())) Catalogs.Add(catalog);
        }
    }

    /// <summary>Forgets every catalog. For tests that need to observe the fallback.</summary>
    public static void Clear()
    {
        lock (Gate)
        {
            Registered.Clear();
            Catalogs.Clear();
        }
    }
}
