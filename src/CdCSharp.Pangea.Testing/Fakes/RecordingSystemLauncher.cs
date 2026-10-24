using CdCSharp.Pangea.Platform;

namespace CdCSharp.Pangea.Testing.Fakes;

/// <summary>
/// Remembers what would have been handed to the operating system, without opening anything.
/// </summary>
/// <remarks>
/// A test that reaches the real launcher opens Explorer or a browser on the machine running it -
/// on a build agent, with nobody there to close it.
/// </remarks>
public sealed class RecordingSystemLauncher : ISystemLauncher
{
    /// <summary>What every call answers. <see langword="true"/> by default: the system took it.</summary>
    public bool Succeeds { get; set; } = true;

    /// <summary>Paths asked to be opened, in order.</summary>
    public List<string> Opened { get; } = [];

    /// <summary>Paths asked to be shown in the file manager, in order.</summary>
    public List<string> ShownInFolder { get; } = [];

    /// <summary>Links asked to be opened, in order.</summary>
    public List<Uri> OpenedUris { get; } = [];

    public Task<bool> OpenAsync(string path)
    {
        Opened.Add(path);
        return Task.FromResult(Succeeds);
    }

    public Task<bool> ShowInFolderAsync(string path)
    {
        ShownInFolder.Add(path);
        return Task.FromResult(Succeeds);
    }

    public Task<bool> OpenUriAsync(Uri uri)
    {
        OpenedUris.Add(uri);
        return Task.FromResult(Succeeds);
    }
}
