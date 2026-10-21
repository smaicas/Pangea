using System.Reflection;

namespace CdCSharp.Pangea.Apps;

/// <summary>Who this application is: its manifest, read from the entry assembly.</summary>
/// <param name="Id">Stable, lower case.</param>
/// <param name="Name">What a person sees.</param>
public sealed record AppIdentity(string Id, string Name)
{
    public string? Description { get; init; }

    /// <summary>The version, without the build metadata.</summary>
    public string? Version { get; init; }

    /// <summary>The commit it was built from, when the build stamped one.</summary>
    public string? Commit { get; init; }

    /// <summary>
    /// Reads the manifest of <paramref name="assembly"/>, falling back to its name when it has none -
    /// an application that never set <c>PangeaAppId</c> still runs, just under its assembly name.
    /// </summary>
    public static AppIdentity From(Assembly assembly, AppsOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        PangeaAppAttribute? manifest = assembly.GetCustomAttribute<PangeaAppAttribute>();
        string fallback = assembly.GetName().Name ?? "app";

        (string? version, string? commit) = SplitVersion(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString());

        return new AppIdentity(
            options?.AppId ?? manifest?.Id ?? fallback.ToLowerInvariant(),
            options?.AppName ?? manifest?.Name ?? fallback)
        {
            Description = manifest?.Description,
            Version = version,
            Commit = commit
        };
    }

    /// <summary>The entry assembly's identity, or one built from the options when there is no entry assembly - a test host.</summary>
    public static AppIdentity Resolve(AppsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Assembly.GetEntryAssembly() is { } entry
            ? From(entry, options)
            : new AppIdentity(options.AppId ?? "app", options.AppName ?? options.AppId ?? "app");
    }

    /// <summary>Splits <c>1.2.3+abcdef</c> into the version and the commit the SDK appends.</summary>
    internal static (string? Version, string? Commit) SplitVersion(string? informational)
    {
        if (string.IsNullOrWhiteSpace(informational)) return (null, null);

        int plus = informational.IndexOf('+');

        return plus < 0
            ? (informational, null)
            : (informational[..plus], informational[(plus + 1)..] is { Length: > 0 } commit ? commit : null);
    }
}
