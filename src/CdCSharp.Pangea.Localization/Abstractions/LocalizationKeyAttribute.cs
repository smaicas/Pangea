namespace CdCSharp.Pangea.Localization.Abstractions;

/// <summary>
/// Marks a parameter whose value is a resource key, so the key can be checked against the
/// application's <c>.resx</c> files at compile time.
/// </summary>
/// <remarks>
/// <see cref="ILocalizationService.GetString"/> carries it already. Put it on your own wrappers too
/// - an indexer that forwards to the service, a helper that formats a localized string - and the
/// keys they are called with are checked the same way:
/// <code>
/// public string this[[LocalizationKey] string key] => _localization.GetString(key);
/// </code>
/// Only constant arguments can be checked; a key built at runtime is left alone.
/// </remarks>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class LocalizationKeyAttribute : Attribute;
