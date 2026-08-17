using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CdCSharp.Pangea.Theming.Tests.Infrastructure;

/// <summary>
/// Records where each control dictionary stands relative to the Avalonia Simple theme it was
/// vendored from. The theme is a starting point that is meant to diverge, so this is a ledger of
/// that divergence, not a demand that the files stay identical: a file is either still untouched
/// ("vendored", hash-pinned) or deliberately owned by this repo ("customized", free to change).
/// </summary>
public sealed class UpstreamManifest
{
    public const string VendoredOrigin = "vendored";
    public const string CustomizedOrigin = "customized";

    private const string ManifestFileName = "upstream-manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [JsonPropertyName("avaloniaVersion")]
    public string AvaloniaVersion { get; set; } = "";

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("files")]
    public Dictionary<string, ManifestEntry> Files { get; set; } = new(StringComparer.Ordinal);

    public sealed class ManifestEntry
    {
        [JsonPropertyName("origin")]
        public string Origin { get; set; } = VendoredOrigin;

        /// <summary>Only meaningful for vendored files; customized ones are free to drift.</summary>
        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        /// <summary>Why this repo owns the file. Required for customized entries.</summary>
        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    /// <summary>Path of the manifest inside the working tree (not the copy in bin).</summary>
    public static string Path { get; } = System.IO.Path.Combine(
        ThemeSources.RepositoryRoot, "test", "CdCSharp.Pangea.Theming.Tests", ManifestFileName);

    public static UpstreamManifest Load() =>
        JsonSerializer.Deserialize<UpstreamManifest>(File.ReadAllText(Path), SerializerOptions)
        ?? throw new InvalidOperationException($"'{Path}' is not a readable manifest.");

    public void Save() => File.WriteAllText(Path, JsonSerializer.Serialize(this, SerializerOptions) + "\n");

    /// <summary>
    /// Content hash with line endings normalised, so a checkout with different git autocrlf
    /// settings does not read as drift.
    /// </summary>
    public static string HashOf(string file)
    {
        string normalised = File.ReadAllText(file).Replace("\r\n", "\n").TrimEnd('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalised))).ToLowerInvariant();
    }

    /// <summary>The Avalonia version the theming project builds against.</summary>
    public static string ReferencedAvaloniaVersion()
    {
        string csproj = System.IO.Path.Combine(
            ThemeSources.RepositoryRoot, "src", "CdCSharp.Pangea.Theming", "CdCSharp.Pangea.Theming.csproj");

        Match match = Regex.Match(File.ReadAllText(csproj),
            @"<PackageReference\s+Include=""Avalonia""\s+Version=""([^""]+)""");

        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException("No Avalonia PackageReference found in the theming project.");
    }

    /// <summary>
    /// Rewrites the manifest from the working tree: refreshes vendored hashes, adds unknown files as
    /// vendored, drops entries whose file is gone, and leaves customized entries alone. Run after a
    /// re-vendor with PANGEA_UPDATE_THEME_MANIFEST=1.
    /// </summary>
    public static UpstreamManifest Regenerate()
    {
        UpstreamManifest manifest = File.Exists(Path) ? Load() : new UpstreamManifest();
        manifest.AvaloniaVersion = ReferencedAvaloniaVersion();
        manifest.Note ??= "Control dictionaries vendored from Avalonia's Simple theme as a starting point. "
                          + "'vendored' files are still byte-identical to upstream and hash-pinned; "
                          + "'customized' files are owned by this repo and may diverge freely.";

        Dictionary<string, ManifestEntry> refreshed = new(StringComparer.Ordinal);

        foreach (string file in ThemeSources.ControlDictionaries())
        {
            string name = System.IO.Path.GetFileName(file);
            ManifestEntry entry = manifest.Files.TryGetValue(name, out ManifestEntry? existing)
                ? existing
                : new ManifestEntry();

            if (entry.Origin == CustomizedOrigin)
            {
                entry.Sha256 = null;
            }
            else
            {
                entry.Origin = VendoredOrigin;
                entry.Sha256 = HashOf(file);
                entry.Reason = null;
            }

            refreshed[name] = entry;
        }

        manifest.Files = refreshed
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);

        return manifest;
    }
}
