using System.Text.RegularExpressions;

namespace PipelineRunner.Services;

/// <summary>
/// Parses a Keep-a-Changelog style CHANGELOG.md into version sections
/// and provides query helpers used by <see cref="ChangelogGate"/>.
/// </summary>
internal static partial class ChangelogParser
{
    [GeneratedRegex(@"^## \[([^\]]+)\]", RegexOptions.Multiline)]
    private static partial Regex SectionHeaderPattern();

    /// <summary>
    /// Splits CHANGELOG content into (Version, Content) sections ordered by appearance.
    /// </summary>
    internal static IReadOnlyList<ChangelogSection> ParseSections(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<ChangelogSection>();
        }

        var sections = new List<ChangelogSection>();
        var matches = SectionHeaderPattern().Matches(content);

        for (var i = 0; i < matches.Count; i++)
        {
            var version = matches[i].Groups[1].Value.Trim();
            var contentStart = matches[i].Index + matches[i].Length;
            var contentEnd = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;
            var sectionContent = content[contentStart..contentEnd].Trim();
            sections.Add(new ChangelogSection(version, sectionContent));
        }

        return sections;
    }

    /// <summary>
    /// Returns true when <paramref name="version"/> is relevant for the given <paramref name="baseline"/>:
    /// i.e. the version is "Unreleased" or its numeric part is greater than or equal to the baseline.
    /// Versions that cannot be parsed are treated as relevant (conservative fallback).
    /// </summary>
    internal static bool IsVersionRelevantFor(string version, string baseline)
    {
        if (string.Equals(version, "Unreleased", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryParseNumericVersion(version, out var v) && TryParseNumericVersion(baseline, out var b))
        {
            return v >= b;
        }

        // Cannot parse — be conservative and include the section
        return true;
    }

    /// <summary>
    /// Returns true when the namespace name appears (case-insensitive) anywhere in the section content.
    /// </summary>
    internal static bool HasMentionOf(string sectionContent, string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(sectionContent) || string.IsNullOrWhiteSpace(namespaceName))
        {
            return false;
        }

        return sectionContent.Contains(namespaceName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Strips pre-release suffixes (e.g. "-rc.2.25502.107") and parses the numeric part.
    /// </summary>
    private static bool TryParseNumericVersion(string versionString, out Version? version)
    {
        var dashIndex = versionString.IndexOf('-', StringComparison.Ordinal);
        var spaceIndex = versionString.IndexOf(' ', StringComparison.Ordinal);

        // Use the earliest delimiter found (dash or space before date in CHANGELOG headers)
        var cutIndex = dashIndex >= 0 && spaceIndex >= 0
            ? Math.Min(dashIndex, spaceIndex)
            : dashIndex >= 0 ? dashIndex
            : spaceIndex >= 0 ? spaceIndex
            : -1;

        var numericPart = cutIndex >= 0 ? versionString[..cutIndex] : versionString;
        return Version.TryParse(numericPart.Trim(), out version);
    }
}

/// <summary>A parsed section from a CHANGELOG, identified by version tag and content.</summary>
internal sealed record ChangelogSection(string Version, string Content);
