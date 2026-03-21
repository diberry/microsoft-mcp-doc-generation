// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Deterministic post-processing that injects required Microsoft Learn frontmatter
/// fields into tool-family articles. Fields that already exist are preserved.
/// 
/// Required fields per Microsoft Learn publishing standards:
/// - author, ms.author, ms.date, ms.service, ms.topic
/// - ai-usage (for AI-generated content)
/// - content_well_notification (for AI-generated content)
/// - ms.custom (for campaign tracking)
/// </summary>
public static class FrontmatterEnricher
{
    private const string Author = "diberry";
    private const string AiUsage = "ai-generated";
    private const string ContentWellValue = "AI-contribution";
    private const string MsCustom = "build-2025";

    /// <summary>
    /// Injects missing required frontmatter fields. Preserves existing fields.
    /// Idempotent — safe to call multiple times.
    /// </summary>
    public static string Enrich(string markdown)
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

        // Inject missing fields
        InjectIfMissing(lines, "author", $"author: {Author}");
        InjectIfMissing(lines, "ms.author", $"ms.author: {Author}");
        InjectIfMissing(lines, "ms.date", $"ms.date: {DateTime.UtcNow:MM/dd/yyyy}");
        InjectIfMissing(lines, "ai-usage", $"ai-usage: {AiUsage}");
        InjectIfMissing(lines, "ms.custom", $"ms.custom: {MsCustom}");

        // content_well_notification is special (multi-line YAML array)
        if (!lines.Any(l => l.TrimStart().StartsWith("content_well_notification")))
        {
            lines.Add("content_well_notification:");
            lines.Add($"  - {ContentWellValue}");
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
