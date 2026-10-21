namespace CdCSharp.Pangea.Apps;

/// <summary>
/// What a host decides for every application connected to it: the parameters they share.
/// </summary>
/// <remarks>
/// The toolkit defines which parameters exist and how they travel; the host decides their values
/// and sends them on connection and again whenever one changes. An application running on its own
/// has no environment and uses its own settings.
/// </remarks>
public sealed record AppEnvironment
{
    /// <summary>Who decided: the host's name, for a status line.</summary>
    public string? HostName { get; init; }

    /// <summary>The look to wear, or null to keep the application's own.</summary>
    public AppThemeSnapshot? Theme { get; init; }

    /// <summary>A culture name such as <c>es-ES</c>, or null to keep the application's own.</summary>
    public string? Culture { get; init; }
}

/// <summary>Light, dark, or whatever the system says.</summary>
public enum AppThemeVariant
{
    System,
    Light,
    Dark
}

/// <summary>
/// A theme as data: both palettes by resource key, so it can leave the process that owns it.
/// </summary>
/// <param name="Light">Colours of the light variant, by resource key, as <c>#AARRGGBB</c>.</param>
/// <param name="Dark">Colours of the dark variant, by resource key, as <c>#AARRGGBB</c>.</param>
public sealed record AppThemeSnapshot(IReadOnlyDictionary<string, string> Light, IReadOnlyDictionary<string, string> Dark)
{
    public AppThemeVariant Variant { get; init; } = AppThemeVariant.System;

    public double PickerHighlightOpacity { get; init; } = 0.2d;
}
