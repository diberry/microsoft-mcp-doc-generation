// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Deterministic post-processing that injects required Microsoft Learn frontmatter
/// fields into tool-family articles. Fields that already exist are preserved.
/// 
/// Required fields per Microsoft Learn publishing standards:
/// - author, ms.author, ms.reviewer, ms.date, ms.service, ms.topic
/// - ai-usage (for AI-generated content)
/// - content_well_notification (for AI-generated content)
/// - ms.custom (for campaign tracking)
/// 
/// Phase 0: Converted to instance class with clock injection for testability.
/// </summary>
public class FrontmatterEnricher
{
    private readonly Func<DateTime> _clock;

    /// <summary>
    /// Creates a new instance of FrontmatterEnricher.
    /// </summary>
    /// <param name="clock">Optional clock function for date generation. Defaults to DateTime.UtcNow.</param>
    public FrontmatterEnricher(Func<DateTime>? clock = null)
    {
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Static convenience method for backward compatibility.
    /// Creates a new instance with default clock and enriches the markdown.
    /// </summary>
    public static string EnrichWithDefaults(string markdown)
    {
        var enricher = new FrontmatterEnricher();
        return enricher.Enrich(markdown);
    }

    /// <summary>
    /// Injects missing required frontmatter fields. Preserves existing fields.
    /// Idempotent — safe to call multiple times.
    /// </summary>
    public string Enrich(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return markdown;
        }

        // Must start with frontmatter
        if (!markdown.StartsWith("---"))
        {
            return markdown;
        }

        // Find the closing --- of frontmatter
        int endMarkerIndex = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endMarkerIndex < 0)
        {
            return markdown;
        }

        // Extract frontmatter content (between the --- markers)
        string frontmatter = markdown.Substring(4, endMarkerIndex - 4); // skip opening "---\n"
        string body = markdown.Substring(endMarkerIndex + 4); // skip "\n---"

        // Parse existing fields
        var lines = frontmatter.Split('\n').ToList();

        // Inject missing fields (using MetadataConstants)
        InjectIfMissing(lines, "author", $"author: {MetadataConstants.Author}");
        InjectIfMissing(lines, "ms.author", $"ms.author: {MetadataConstants.Author}");
        InjectIfMissing(lines, "ms.reviewer", string.IsNullOrEmpty(MetadataConstants.Reviewer) ? "ms.reviewer:" : $"ms.reviewer: {MetadataConstants.Reviewer}");
        InjectIfMissing(lines, "ms.date", $"ms.date: {_clock():MM/dd/yyyy}");
        InjectIfMissing(lines, "ai-usage", $"ai-usage: {MetadataConstants.AiUsage}");
        InjectIfMissing(lines, "ms.custom", $"ms.custom: {MetadataConstants.MsCustom}");

        // content_well_notification is special (multi-line YAML array)
        if (!lines.Any(l => l.TrimStart().StartsWith("content_well_notification")))
        {
            lines.Add("content_well_notification:");
            lines.Add($"  - {MetadataConstants.ContentWellValue}");
        }

        // Reassemble
        var sb = new StringBuilder();
        sb.AppendLine("---");
        foreach (var line in lines)
        {
            sb.AppendLine(line);
        }
        sb.Append("---");
        sb.Append(body);

        return sb.ToString();
    }

    private static void InjectIfMissing(List<string> lines, string key, string fullLine)
    {
        // Check if any line starts with "key:" (with or without leading whitespace for nested YAML)
        if (!lines.Any(l => l.TrimStart().StartsWith($"{key}:", StringComparison.Ordinal)))
        {
            lines.Add(fullLine);
        }
    }
}
