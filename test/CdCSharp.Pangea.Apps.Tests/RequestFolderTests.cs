using Microsoft.Extensions.Logging.Abstractions;

namespace CdCSharp.Pangea.Apps.Tests;

/// <summary>
/// Files written for another application leave no trace: a folder per request, the caller's to
/// delete, and the forgotten ones deleted later.
/// </summary>
public sealed class RequestFolderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pangea-request-folders-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private AppRequestFolders Folders(TimeSpan? kept = null) => new(_root, NullLogger<AppRequestFolders>.Instance, kept);

    [Fact]
    public void Each_request_gets_a_new_empty_folder_inside_the_root()
    {
        AppRequestFolders folders = Folders();

        string first = folders.NewFolder();
        string second = folders.NewFolder();

        Assert.NotEqual(first, second);
        Assert.True(Directory.Exists(first));
        Assert.Empty(Directory.EnumerateFileSystemEntries(first));
        Assert.Equal(folders.Root, Path.GetDirectoryName(first));
    }

    [Fact]
    public void Folders_older_than_kept_are_deleted_when_a_new_one_is_made()
    {
        AppRequestFolders folders = Folders(TimeSpan.FromHours(1));

        string forgotten = folders.NewFolder();
        File.WriteAllText(Path.Combine(forgotten, "song.mp3"), "");
        Directory.SetCreationTimeUtc(forgotten, DateTime.UtcNow.AddHours(-2));

        string recent = folders.NewFolder();
        string newest = folders.NewFolder();

        Assert.False(Directory.Exists(forgotten));
        Assert.True(Directory.Exists(recent));
        Assert.True(Directory.Exists(newest));
    }

    [Fact]
    public void Deleting_takes_the_folder_and_its_files()
    {
        AppRequestFolders folders = Folders();
        string folder = folders.NewFolder();
        File.WriteAllText(Path.Combine(folder, "result.png"), "");

        folders.Delete(folder);

        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void A_folder_outside_the_root_is_never_deleted()
    {
        AppRequestFolders folders = Folders();
        string elsewhere = Path.Combine(_root + "-elsewhere", "keep");
        Directory.CreateDirectory(elsewhere);

        try
        {
            folders.Delete(elsewhere);
            folders.Delete(_root + "-elsewhere");

            Assert.True(Directory.Exists(elsewhere));
        }
        finally
        {
            Directory.Delete(_root + "-elsewhere", recursive: true);
        }
    }

    [Fact]
    public void The_default_root_is_the_applications_own_among_the_temporary_files()
    {
        string root = AppRequestFolders.DefaultRootFor(new AppIdentity("yutuf", "Yutuf"));

        Assert.Equal(Path.Combine(Path.GetTempPath(), "yutuf", "requests"), root);
    }

    [Fact]
    public void The_caller_deletes_the_folder_of_a_result_once_it_has_what_it_wanted()
    {
        string folder = Folders().NewFolder();
        File.WriteAllText(Path.Combine(folder, "song.mp3"), "");

        Assert.True(new FilesAnswer(folder).DeleteFolder());
        Assert.False(Directory.Exists(folder));

        // Nothing written, or already gone: nothing to do, and no exception.
        Assert.True(new FilesAnswer(null).DeleteFolder());
        Assert.True(new FilesAnswer(folder).DeleteFolder());
    }

    private sealed record FilesAnswer(string? Folder) : IAppFilesResult;
}
