namespace CdCSharp.Pangea.Files.Tests;

/// <summary>Names from a template, and a name taken only once however many workers ask at once.</summary>
public sealed class OutputNameTests : IDisposable
{
    private readonly Workspace _files = new();

    public void Dispose() => _files.Dispose();

    [Fact]
    public void A_template_fills_in_the_original_the_position_the_size_and_the_format()
    {
        Assert.Equal("holiday-007-640x480.webp", OutputNames.Apply("{name}-{nnn}-{w}x{h}.{format}", Path.Combine("photos", "holiday.jpg"), 7, 640, 480, "webp"));
        Assert.Equal("holiday", OutputNames.Apply("", "holiday.jpg", 1));
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), OutputNames.Apply("{date}", "a.jpg", 1));
    }

    [Fact]
    public void A_name_is_made_safe_for_any_desktop_and_never_empty()
    {
        Assert.Equal("a_b_c_d", OutputNames.Sanitize("a:b*c?d"));
        Assert.Equal("report", OutputNames.Sanitize("report.. "));
        Assert.Equal("image", OutputNames.Sanitize(" . ", fallback: "image"));
        Assert.Equal("x_y", OutputNames.Sanitize("x/y"));
    }

    [Fact]
    public void Renaming_counts_up_past_what_exists()
    {
        string folder = _files.Folder("out");
        _files.File("out/photo.png");
        _files.File("out/photo (2).png");

        string? claimed = OutputNames.Claim(folder, "photo", ".png", ConflictPolicy.Rename);

        Assert.Equal(Path.Combine(folder, "photo (3).png"), claimed);
        Assert.True(File.Exists(claimed));
    }

    [Fact]
    public void Skipping_gives_nothing_for_a_name_that_exists_and_overwriting_gives_it_back()
    {
        string folder = _files.Folder("out");
        string existing = _files.File("out/photo.png", bytes: 5);

        Assert.Null(OutputNames.Claim(folder, "photo", ".png", ConflictPolicy.Skip));
        Assert.Equal(existing, OutputNames.Claim(folder, "photo", ".png", ConflictPolicy.Overwrite));
        Assert.Equal(5, new FileInfo(existing).Length);
    }

    [Fact]
    public void A_missing_folder_is_made()
    {
        string folder = Path.Combine(_files.Root, "new", "deeper");

        Assert.Equal(Path.Combine(folder, "a.png"), OutputNames.Claim(folder, "a", ".png", ConflictPolicy.Rename));
    }

    [Fact]
    public void Workers_asking_for_the_same_name_at_once_each_get_their_own()
    {
        string folder = _files.Folder("out");
        string?[] claimed = new string?[64];

        Parallel.For(0, claimed.Length, index => claimed[index] = OutputNames.Claim(folder, "frame", ".png", ConflictPolicy.Rename));

        Assert.All(claimed, Assert.NotNull);
        Assert.Equal(claimed.Length, claimed.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(claimed.Length, Directory.GetFiles(folder).Length);
    }
}
