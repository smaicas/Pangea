using PangeaDataApp.Domain;

namespace PangeaDataApp.Tests;

/// <summary>
/// The rules, asked directly.
/// </summary>
/// <remarks>
/// No database, no dispatcher, no window - which is the whole argument for keeping them under
/// <c>Domain</c>. A rule reachable only through a screen is a rule that gets tested by clicking,
/// and a rule tested by clicking is a rule nobody rechecks after changing it.
/// </remarks>
public class NoteDraftTests
{
    [Fact]
    public void ATitle_IsTrimmed()
    {
        NoteDraft draft = Assert.IsType<NoteDraft>(NoteDraft.From("  Shopping list  ", null));

        Assert.Equal("Shopping list", draft.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyTitle_IsNoNoteAtAll(string? title) => Assert.Null(NoteDraft.From(title, "a body"));

    /// <summary>A body of spaces is a body nobody wrote, and null is what the column means by that.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankBody_BecomesNull(string body) => Assert.Null(NoteDraft.From("Title", body)?.Body);

    [Fact]
    public void ABody_IsTrimmed() => Assert.Equal("Milk", NoteDraft.From("Title", "  Milk  ")?.Body);
}

public class FileSizeTests
{
    [Theory]
    [InlineData(null, "no file")]
    [InlineData(512L, "512 B")]
    [InlineData(2048L, "2 KB")]
    // The case the summary used to get wrong: a database in daily use is megabytes, and a number
    // in kilobytes is one the reader has to divide in their head before it means anything.
    [InlineData(40L * 1024 * 1024, "40 MB")]
    [InlineData(3L * 1024 * 1024 * 1024, "3 GB")]
    public void ASize_IsScaledToSomethingReadable(long? bytes, string expected) =>
        Assert.Equal(expected, FileSize.Describe(bytes));

    /// <summary>
    /// The number is written in the reader's own format, so the expectation has to be too - a test
    /// spelling "1.5 KB" passes in London and fails in Madrid, where it reads 1,5.
    /// </summary>
    [Fact]
    public void AFractionalSize_UsesTheReadersNumberFormat() =>
        Assert.Equal($"{1.5:0.#} KB", FileSize.Describe(1536));
}
