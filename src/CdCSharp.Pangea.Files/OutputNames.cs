using System.Globalization;
using System.Text.RegularExpressions;

namespace CdCSharp.Pangea.Files;

/// <summary>What to do when the file a result would be written to already exists.</summary>
public enum ConflictPolicy
{
    /// <summary>Add a number: «photo (2).jpg».</summary>
    Rename,

    Overwrite,

    /// <summary>Leave the existing file, and the source with it.</summary>
    Skip
}

/// <summary>
/// Names the files a batch writes, from a template, and takes the name so no other worker can.
/// </summary>
/// <remarks>
/// <para>
/// In a template <c>{name}</c> is the original's name, <c>{n}</c> the position in the batch -
/// <c>{nnn}</c> with zeros - <c>{w}</c> and <c>{h}</c> the result's size, <c>{format}</c> its format,
/// <c>{date}</c> and <c>{time}</c> now.
/// </para>
/// <para>
/// <see cref="Claim"/> creates the file it returns, empty, so two workers resolving the same name
/// at once get two names; the result is then written over the placeholder. It is atomic on the
/// file system, so it holds across threads and processes alike.
/// </para>
/// </remarks>
public static partial class OutputNames
{
    public const string DefaultTemplate = "{name}";

    /// <summary>The name a template gives, without folder or extension, safe for a file system.</summary>
    /// <param name="template">The template; <see cref="DefaultTemplate"/> when empty.</param>
    /// <param name="source">The original file, for <c>{name}</c>.</param>
    /// <param name="index">The position in the batch, from 1, for <c>{n}</c>.</param>
    /// <param name="width">For <c>{w}</c>.</param>
    /// <param name="height">For <c>{h}</c>.</param>
    /// <param name="format">For <c>{format}</c>.</param>
    /// <param name="fallback">The name when the template comes to nothing usable.</param>
    public static string Apply(string? template, string source, int index, int width = 0, int height = 0, string? format = null, string fallback = "file")
    {
        DateTime now = DateTime.Now;
        string text = string.IsNullOrWhiteSpace(template) ? DefaultTemplate : template;

        text = Counter().Replace(text, match => index.ToString(new string('0', match.Groups[1].Length), CultureInfo.InvariantCulture));
        text = text
            .Replace("{name}", Path.GetFileNameWithoutExtension(source), StringComparison.OrdinalIgnoreCase)
            .Replace("{w}", width.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{h}", height.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", now.ToString("HHmmss", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{format}", format ?? "", StringComparison.OrdinalIgnoreCase);

        return Sanitize(text, fallback);
    }

    /// <summary>
    /// A name every desktop file system accepts: no reserved characters, no trailing dots or spaces.
    /// </summary>
    public static string Sanitize(string name, string fallback = "file")
    {
        ArgumentNullException.ThrowIfNull(name);

        char[] invalid = Path.GetInvalidFileNameChars();
        string clean = new(name.Select(c => invalid.Contains(c) || c is ':' or '*' or '?' or '"' or '<' or '>' or '|' or '/' or '\\' ? '_' : c).ToArray());
        clean = clean.Trim().TrimEnd('.').Trim();

        return clean.Length == 0 ? fallback : clean;
    }

    /// <summary>
    /// Takes <paramref name="name"/> + <paramref name="extension"/> in <paramref name="folder"/> for a
    /// result, following <paramref name="policy"/> when it exists.
    /// </summary>
    /// <param name="folder">Created when missing.</param>
    /// <param name="name">From <see cref="Apply"/>, without extension.</param>
    /// <param name="extension">With its dot: <c>.png</c>.</param>
    /// <param name="policy">What to do when the file is there.</param>
    /// <returns>
    /// The path to write to - created empty unless overwriting - or <see langword="null"/> when it
    /// exists and the policy says to skip.
    /// </returns>
    public static string? Claim(string folder, string name, string extension, ConflictPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(extension);

        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name + extension);

        if (policy == ConflictPolicy.Overwrite) return path;
        if (TryCreate(path)) return path;
        if (policy == ConflictPolicy.Skip) return null;

        for (int copy = 2; ; copy++)
        {
            string candidate = Path.Combine(folder, $"{name} ({copy}){extension}");
            if (TryCreate(candidate)) return candidate;
        }
    }

    /// <summary><see cref="Apply"/> and <see cref="Claim"/> in one: the path a batch writes a result to.</summary>
    public static string? Resolve(string? template, string source, int index, string folder, string extension, ConflictPolicy policy,
        int width = 0, int height = 0, string? format = null, string fallback = "file") =>
        Claim(folder, Apply(template, source, index, width, height, format, fallback), extension, policy);

    /// <summary>Creates the file only if nobody has: the one step two workers cannot both win.</summary>
    private static bool TryCreate(string path)
    {
        try
        {
            using FileStream _ = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            return true;
        }
        catch (IOException) when (File.Exists(path) || Directory.Exists(path))
        {
            return false;
        }
    }

    [GeneratedRegex(@"\{(n+)\}", RegexOptions.IgnoreCase)]
    private static partial Regex Counter();
}
