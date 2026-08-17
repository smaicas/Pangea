using CdCSharp.Pangea.Theming.Tests.Infrastructure;

namespace CdCSharp.Pangea.Theming.Tests;

/// <summary>
/// L4 - keeps the vendored-copy situation honest. The control dictionaries came from Avalonia's
/// Simple theme as a starting point, so these tests do not forbid divergence; they make it explicit.
/// Editing a still-vendored file, adding a dictionary, or bumping Avalonia without re-vendoring all
/// fail here rather than being discovered months later.
///
/// After a re-vendor: run the suite with PANGEA_UPDATE_THEME_MANIFEST=1 to refresh the manifest.
/// </summary>
public class VendoredThemeDriftTests
{
    private const string UpdateSwitch = "PANGEA_UPDATE_THEME_MANIFEST";

    private static bool RegenerationRequested =>
        Environment.GetEnvironmentVariable(UpdateSwitch) is "1" or "true";

    [Fact]
    public void ManifestCoversEveryControlDictionary()
    {
        if (TryRegenerate()) return;

        UpstreamManifest manifest = UpstreamManifest.Load();

        List<string> onDisk = ThemeSources.ControlDictionaries().Select(Path.GetFileName).ToList()!;

        List<string> undeclared = onDisk.Where(name => !manifest.Files.ContainsKey(name!)).ToList();
        List<string> vanished = manifest.Files.Keys.Where(name => !onDisk.Contains(name)).ToList();

        Assert.True(undeclared.Count == 0,
            $"Dictionaries exist but are not declared in the manifest. Re-run with {UpdateSwitch}=1 " +
            "after checking they really came from upstream: " + string.Join(", ", undeclared));

        Assert.True(vanished.Count == 0,
            $"The manifest lists dictionaries that no longer exist. Re-run with {UpdateSwitch}=1: " +
            string.Join(", ", vanished));
    }

    [Fact]
    public void VendoredDictionaries_AreStillUntouched()
    {
        if (TryRegenerate()) return;

        UpstreamManifest manifest = UpstreamManifest.Load();

        List<string> drifted = manifest.Files
            .Where(entry => entry.Value.Origin == UpstreamManifest.VendoredOrigin)
            .Where(entry =>
            {
                string file = Path.Combine(ThemeSources.SharedDirectory, entry.Key);
                return File.Exists(file) && UpstreamManifest.HashOf(file) != entry.Value.Sha256;
            })
            .Select(entry => entry.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(drifted.Count == 0,
            "These dictionaries are recorded as untouched copies of Avalonia's Simple theme but have " +
            "been edited. Either mark them \"customized\" with a reason in the manifest (this repo now " +
            $"owns them), or re-vendor and refresh with {UpdateSwitch}=1: " + string.Join(", ", drifted));
    }

    [Fact]
    public void CustomizedDictionaries_ExplainWhy()
    {
        if (TryRegenerate()) return;

        UpstreamManifest manifest = UpstreamManifest.Load();

        List<string> unexplained = manifest.Files
            .Where(entry => entry.Value.Origin == UpstreamManifest.CustomizedOrigin)
            .Where(entry => string.IsNullOrWhiteSpace(entry.Value.Reason))
            .Select(entry => entry.Key)
            .ToList();

        Assert.True(unexplained.Count == 0,
            "Customized dictionaries need a reason, so the next person knows what to preserve when " +
            "re-vendoring: " + string.Join(", ", unexplained));
    }

    [Fact]
    public void ManifestTracksTheAvaloniaVersionTheProjectBuildsAgainst()
    {
        if (TryRegenerate()) return;

        UpstreamManifest manifest = UpstreamManifest.Load();
        string referenced = UpstreamManifest.ReferencedAvaloniaVersion();

        Assert.True(manifest.AvaloniaVersion == referenced,
            $"The theme was vendored from Avalonia {manifest.AvaloniaVersion} but the project now builds " +
            $"against {referenced}. Upstream may have added, removed or reworked control themes; re-vendor " +
            $"the dictionaries and refresh with {UpdateSwitch}=1.");
    }

    private static bool TryRegenerate()
    {
        if (!RegenerationRequested) return false;

        UpstreamManifest.Regenerate().Save();
        return true;
    }
}
