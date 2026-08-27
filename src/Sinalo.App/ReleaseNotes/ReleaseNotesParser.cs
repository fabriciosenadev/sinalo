using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Sinalo.App.ReleaseNotes;

public sealed record ReleaseNotesDocument(IReadOnlyList<ReleaseNotesVersion> Versions);
public sealed record ReleaseNotesVersion(string Version, string Date, IReadOnlyList<ReleaseNotesSection> Sections);
public sealed record ReleaseNotesSection(string Title, IReadOnlyList<string> Items);

public static class ReleaseNotesParser
{
    private static readonly Regex VersionHeading = new(@"^## (?<version>\d+\.\d+\.\d+)(?:\s+-\s+(?<date>.+))?$", RegexOptions.Compiled);

    public static ReleaseNotesDocument Parse(string markdown)
    {
        var versions = new List<VersionBuilder>();
        VersionBuilder? version = null;
        SectionBuilder? section = null;
        var currentItem = -1;

        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            var heading = VersionHeading.Match(line);
            if (heading.Success)
            {
                version = new VersionBuilder(heading.Groups["version"].Value, heading.Groups["date"].Value);
                versions.Add(version);
                section = null;
                currentItem = -1;
                continue;
            }

            if (version is null) continue;
            if (line.StartsWith("## ")) break;
            if (line.StartsWith("### "))
            {
                section = new SectionBuilder(line[4..]);
                version.Sections.Add(section);
                currentItem = -1;
                continue;
            }

            if (section is null) continue;
            if (line.StartsWith("- "))
            {
                section.Items.Add(Clean(line[2..]));
                currentItem = section.Items.Count - 1;
            }
            else if (!string.IsNullOrWhiteSpace(line) && currentItem >= 0)
            {
                section.Items[currentItem] = $"{section.Items[currentItem]} {Clean(line)}";
            }
        }

        return new ReleaseNotesDocument(versions.Select(item => new ReleaseNotesVersion(
            item.Version,
            item.Date,
            item.Sections.Select(section => new ReleaseNotesSection(section.Title, section.Items)).ToArray())).ToArray());
    }

    private static string Clean(string value) => value.Replace("**", string.Empty).Trim();

    private sealed class VersionBuilder(string version, string date)
    {
        public string Version { get; } = version;
        public string Date { get; } = date;
        public List<SectionBuilder> Sections { get; } = [];
    }

    private sealed class SectionBuilder(string title)
    {
        public string Title { get; } = title;
        public List<string> Items { get; } = [];
    }
}

public static class ReleaseNotesLoader
{
    private const string ResourceName = "Sinalo.App.CHANGELOG.md";

    public static ReleaseNotesDocument Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (stream is null) return new ReleaseNotesDocument([]);
        using var reader = new StreamReader(stream);
        return ReleaseNotesParser.Parse(reader.ReadToEnd());
    }
}
