using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace CdCSharp.Pangea.Localization.CodeAnalysis;

/// <summary>One <c>.resx</c> file, and where each of its keys is written.</summary>
internal sealed class ResourceFile
{
    public ResourceFile(string path, string? culture, Dictionary<string, Location> keys)
    {
        Path = path;
        Culture = culture;
        Keys = keys;
    }

    public string Path { get; }

    /// <summary>The culture from the file name, or <see langword="null"/> for the neutral file.</summary>
    public string? Culture { get; }

    public Dictionary<string, Location> Keys { get; }

    public string FileName => System.IO.Path.GetFileName(Path);
}

/// <summary>A neutral <c>.resx</c> and its translations: <c>Strings.resx</c> beside <c>Strings.es.resx</c>.</summary>
internal sealed class ResourceGroup
{
    public ResourceGroup(ResourceFile? neutral, List<ResourceFile> translations)
    {
        Neutral = neutral;
        Translations = translations;
    }

    public ResourceFile? Neutral { get; }

    public List<ResourceFile> Translations { get; }
}

/// <summary>
/// The project's resource files, read once per compilation.
/// </summary>
/// <remarks>
/// Built from the additional files rather than from disk: an analyzer is handed the files the
/// compilation was given, and reading anything else would report on a project state the build
/// never saw.
/// </remarks>
internal sealed class ResourceIndex
{
    /// <summary>
    /// What separates <c>Strings.es-ES.resx</c> from a file that merely has a dot in its name.
    /// </summary>
    private static readonly Regex CultureName = new(
        @"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$", RegexOptions.CultureInvariant);

    private ResourceIndex(List<ResourceGroup> groups, HashSet<string> allKeys)
    {
        Groups = groups;
        AllKeys = allKeys;
    }

    public List<ResourceGroup> Groups { get; }

    /// <summary>Every key defined anywhere, which is what a lookup can possibly resolve.</summary>
    public HashSet<string> AllKeys { get; }

    /// <summary>
    /// No resource files at all means nothing to check against. Reporting every key as missing
    /// would be the analyzer describing its own blindness.
    /// </summary>
    public bool IsEmpty => Groups.Count == 0;

    public static ResourceIndex Build(IEnumerable<AdditionalText> additionalFiles, CancellationToken cancellationToken)
    {
        Dictionary<string, (ResourceFile? Neutral, List<ResourceFile> Translations)> groups =
            new(StringComparer.OrdinalIgnoreCase);

        HashSet<string> allKeys = new(StringComparer.Ordinal);

        foreach (AdditionalText file in additionalFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!file.Path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)) continue;

            if (!TryDescribe(file.Path, out string groupKey, out string? culture)) continue;

            SourceText? text = file.GetText(cancellationToken);
            if (text is null) continue;

            // A file with no keys still counts: an empty translation is precisely the state
            // PGL002 exists to describe. Only one that could not be read is skipped.
            Dictionary<string, Location>? keys = ReadKeys(file.Path, text);
            if (keys is null) continue;

            foreach (string key in keys.Keys) allKeys.Add(key);

            if (!groups.TryGetValue(groupKey, out (ResourceFile? Neutral, List<ResourceFile> Translations) group))
            {
                group = (null, new List<ResourceFile>());
            }

            ResourceFile resource = new(file.Path, culture, keys);

            groups[groupKey] = culture is null
                ? (resource, group.Translations)
                : (group.Neutral, Append(group.Translations, resource));
        }

        List<ResourceGroup> built = groups.Values
            .Select(group => new ResourceGroup(group.Neutral, group.Translations))
            .ToList();

        return new ResourceIndex(built, allKeys);
    }

    private static List<ResourceFile> Append(List<ResourceFile> translations, ResourceFile resource)
    {
        translations.Add(resource);
        return translations;
    }

    /// <summary>
    /// Splits <c>Strings.resx</c> and <c>Strings.es.resx</c> into the group they share and the
    /// culture that tells them apart.
    /// </summary>
    private static bool TryDescribe(string path, out string groupKey, out string? culture)
    {
        groupKey = string.Empty;
        culture = null;

        string fileName = Path.GetFileNameWithoutExtension(path);
        string directory = Path.GetDirectoryName(path) ?? string.Empty;

        int separator = fileName.LastIndexOf('.');

        if (separator < 0)
        {
            groupKey = directory + "|" + fileName;
            return true;
        }

        string suffix = fileName.Substring(separator + 1);

        // A dot that is not a culture belongs to the name: 'App.Strings.resx' is its own group,
        // not a translation of 'App.resx' into a language called 'Strings'.
        if (!CultureName.IsMatch(suffix))
        {
            groupKey = directory + "|" + fileName;
            return true;
        }

        groupKey = directory + "|" + fileName.Substring(0, separator);
        culture = suffix;
        return true;
    }

    /// <summary>
    /// Reads the <c>&lt;data name="..."&gt;</c> entries, pointing each one at where it is written.
    /// </summary>
    /// <remarks>
    /// A malformed file yields <see langword="null"/> rather than throwing, and is then left out
    /// entirely: the compiler already reports it, and reading it as an empty file would have every
    /// key in the project reported as untranslated on top of that.
    /// </remarks>
    private static Dictionary<string, Location>? ReadKeys(string path, SourceText text)
    {
        Dictionary<string, Location> keys = new(StringComparer.Ordinal);

        XDocument document;

        try
        {
            document = XDocument.Parse(text.ToString(), LoadOptions.SetLineInfo);
        }
        catch (XmlException)
        {
            return null;
        }

        if (document.Root is null) return null;

        foreach (XElement data in document.Root.Elements("data"))
        {
            XAttribute? name = data.Attribute("name");

            if (name?.Value is not { Length: > 0 } key || keys.ContainsKey(key)) continue;

            keys[key] = LocationOf(path, text, name, key);
        }

        return keys;
    }

    /// <summary>Points at the key itself inside the <c>name</c> attribute, where it can be edited.</summary>
    private static Location LocationOf(string path, SourceText text, XAttribute attribute, string key)
    {
        if (attribute is not IXmlLineInfo position || !position.HasLineInfo())
        {
            return Location.Create(path, new TextSpan(0, 0), new LinePositionSpan());
        }

        int lineNumber = position.LineNumber - 1;

        if (lineNumber < 0 || lineNumber >= text.Lines.Count)
        {
            return Location.Create(path, new TextSpan(0, 0), new LinePositionSpan());
        }

        TextLine line = text.Lines[lineNumber];
        string lineText = line.ToString();

        int column = Math.Max(0, Math.Min(position.LinePosition - 1, lineText.Length));
        int offset = lineText.IndexOf(key, column, StringComparison.Ordinal);

        if (offset < 0) offset = column;

        LinePosition start = new(lineNumber, offset);
        LinePosition end = new(lineNumber, offset + key.Length);

        return Location.Create(path, new TextSpan(line.Start + offset, key.Length), new LinePositionSpan(start, end));
    }
}
