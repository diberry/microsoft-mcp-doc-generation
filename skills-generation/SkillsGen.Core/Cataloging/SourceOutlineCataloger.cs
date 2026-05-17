using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SkillsGen.Core.Models;

namespace SkillsGen.Core.Cataloging;

public class SourceOutlineCataloger
{
    private static readonly Regex HeadingRegex =
        new(@"^(#{2,3})\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex MalformedHeadingRegex =
        new(@"^#{2,3}\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

    private readonly ILogger<SourceOutlineCataloger>? _logger;

    public SourceOutlineCataloger(ILogger<SourceOutlineCataloger>? logger = null)
    {
        _logger = logger;
    }

    public SkillOutline Catalog(string skillName, string content)
    {
        if (string.IsNullOrEmpty(content))
            return new SkillOutline { CatalogedAt = DateTime.UtcNow };

        var headings = new List<HeadingEntry>();
        var lines = content.Split('\n');
        var inFencedBlock = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // Toggle fenced-code-block state
            if (line.TrimStart().StartsWith("```"))
            {
                inFencedBlock = !inFencedBlock;
                continue;
            }

            if (inFencedBlock)
                continue;

            // Skip malformed headings (## with no text)
            if (MalformedHeadingRegex.IsMatch(line))
            {
                _logger?.LogWarning("SKILL.md '{SkillName}' has malformed heading (no text): '{Line}'", skillName, line.Trim());
                continue;
            }

            var match = HeadingRegex.Match(line);
            if (!match.Success)
                continue;

            var hashes = match.Groups[1].Value;
            var text = match.Groups[2].Value.Trim();
            var level = hashes.Length;

            var mappedTo = HeadingMappingRules.IsKnown(text)
                ? HeadingMappingRules.GetMapping(text)
                : null;

            headings.Add(new HeadingEntry
            {
                Level = level,
                Text = text,
                MappedTo = mappedTo
            });
        }

        var unmappedCount = headings.Count(h => !HeadingMappingRules.IsKnown(h.Text));

        return new SkillOutline
        {
            Headings = headings,
            UnmappedCount = unmappedCount,
            CatalogedAt = DateTime.UtcNow
        };
    }
}
