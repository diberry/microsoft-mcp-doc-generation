// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;
using SkillsRelevance.Models;

namespace SkillsRelevance.Output;

/// <summary>
/// Writes skill relevance results to markdown files in the output directory.
/// </summary>
public static class SkillsMarkdownWriter
{
    /// <summary>
    /// Writes a summary markdown file for all relevant skills for a service.
    /// </summary>
    public static async Task WriteServiceSummaryAsync(
        string outputDir,
        string serviceName,
        List<SkillInfo> relevantSkills,
        List<SkillSource> sources)
    {
        Directory.CreateDirectory(outputDir);

        var fileName = $"{SanitizeFileName(serviceName)}-skills-relevance.md";
        var filePath = Path.Combine(outputDir, fileName);

        var sb = new StringBuilder();

        // YAML Frontmatter
        sb.AppendLine("---");
        sb.AppendLine($"title: GitHub Copilot Skills for {serviceName}");
        sb.AppendLine($"description: Relevant GitHub Copilot skills for working with {serviceName}");
        sb.AppendLine($"generated: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        sb.AppendLine($"skillCount: {relevantSkills.Count}");
        sb.AppendLine("sources:");
        foreach (var source in sources)
        {
            sb.AppendLine($"  - name: {source.DisplayName}");
            sb.AppendLine($"    url: {source.GetHtmlUrl()}");
        }
        sb.AppendLine("---");
        sb.AppendLine();

        // Header
        sb.AppendLine($"# GitHub Copilot Skills Relevant to {serviceName}");
        sb.AppendLine();
        sb.AppendLine($"> This document lists GitHub Copilot skills that are relevant to **{serviceName}**. ");
        sb.AppendLine($"> Skills are sourced from {sources.Count} repositories and scored for relevance.");
        sb.AppendLine($"> Generated: {DateTime.UtcNow:MMMM dd, yyyy}");
        sb.AppendLine();

        if (relevantSkills.Count == 0)
        {
            sb.AppendLine($"No skills with significant relevance to **{serviceName}** were found in the skill sources.");
            sb.AppendLine();
            sb.AppendLine("## Skill Sources Checked");
            foreach (var source in sources)
            {
                sb.AppendLine($"- [{source.DisplayName}]({source.GetHtmlUrl()})");
            }
        }
        else
        {
            sb.AppendLine($"## Summary");
            sb.AppendLine();
            sb.AppendLine($"Found **{relevantSkills.Count}** relevant skill(s) across {sources.Count} source(s):");
            sb.AppendLine();

            // Table of contents
            sb.AppendLine("| # | Skill Name | Source | Relevance | Last Updated |");
            sb.AppendLine("|---|-----------|--------|-----------|--------------|");
            for (int i = 0; i < relevantSkills.Count; i++)
            {
                var skill = relevantSkills[i];
                var anchor = $"#{SanitizeAnchor(skill.Name)}";
                var relevanceBar = GetRelevanceBar(skill.RelevanceScore);
                var updated = skill.LastUpdated.HasValue ? skill.LastUpdated.Value.ToString("yyyy-MM-dd") : "Unknown";
                sb.AppendLine($"| {i + 1} | [{skill.Name}]({anchor}) | {skill.SourceRepository} | {relevanceBar} ({skill.RelevanceScore:F2}) | {updated} |");
            }
            sb.AppendLine();

            // Detailed sections for each skill
            sb.AppendLine("---");
            sb.AppendLine();

            for (int i = 0; i < relevantSkills.Count; i++)
            {
                var skill = relevantSkills[i];
                WriteSkillSection(sb, skill, i + 1);
            }
        }

        // Sources section
        sb.AppendLine("## Skill Sources");
        sb.AppendLine();
        foreach (var source in sources)
        {
            sb.AppendLine($"### {source.DisplayName}");
            sb.AppendLine($"- **Repository**: [{source.Owner}/{source.Repo}]({source.GetHtmlUrl()})");
            sb.AppendLine($"- **Skills path**: `{(string.IsNullOrEmpty(source.Path) ? "/" : source.Path)}`");
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"  ✅ {fileName} ({relevantSkills.Count} skills)");
    }

    /// <summary>
    /// Writes an index file listing all generated service skill files.
    /// </summary>
    public static async Task WriteIndexAsync(string outputDir, List<string> serviceNames)
    {
        Directory.CreateDirectory(outputDir);
        var filePath = Path.Combine(outputDir, "index.md");

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("title: GitHub Copilot Skills Relevance Index");
        sb.AppendLine($"generated: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("# GitHub Copilot Skills Relevance Index");
        sb.AppendLine();
        sb.AppendLine("This directory contains skill relevance reports for Azure services and MCP namespaces.");
        sb.AppendLine();
        sb.AppendLine("## Generated Reports");
        sb.AppendLine();
        foreach (var name in serviceNames.OrderBy(n => n))
        {
            var fileName = $"{SanitizeFileName(name)}-skills-relevance.md";
            sb.AppendLine($"- [{name}]({fileName})");
        }
        sb.AppendLine();

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteSkillSection(StringBuilder sb, SkillInfo skill, int index)
    {
        sb.AppendLine($"## {skill.Name}");
        sb.AppendLine();

        // Metadata table
        sb.AppendLine("| Field | Value |");
        sb.AppendLine("|-------|-------|");
        sb.AppendLine($"| **Skill Name** | {EscapeMarkdown(skill.Name)} |");
        sb.AppendLine($"| **Source** | [{skill.SourceRepository}]({skill.SourceUrl}) |");
        sb.AppendLine($"| **File** | [{skill.FileName}]({skill.SourceUrl}) |");
        sb.AppendLine($"| **Last Updated** | {(skill.LastUpdated.HasValue ? skill.LastUpdated.Value.ToString("yyyy-MM-dd") : "Not available")} |");
        sb.AppendLine($"| **Relevance Score** | {GetRelevanceBar(skill.RelevanceScore)} ({skill.RelevanceScore:F2}) |");

        if (!string.IsNullOrEmpty(skill.Category))
            sb.AppendLine($"| **Category** | {EscapeMarkdown(skill.Category)} |");

        if (!string.IsNullOrEmpty(skill.Author))
            sb.AppendLine($"| **Author** | {EscapeMarkdown(skill.Author)} |");

        if (!string.IsNullOrEmpty(skill.Version))
            sb.AppendLine($"| **Version** | {EscapeMarkdown(skill.Version)} |");

        sb.AppendLine();

        // Azure Services
        if (skill.AzureServices.Count > 0)
        {
            sb.AppendLine("### Azure Services");
            sb.AppendLine();
            foreach (var svc in skill.AzureServices)
            {
                sb.AppendLine($"- {svc}");
            }
            sb.AppendLine();
        }

        // Purpose/Description
        if (!string.IsNullOrEmpty(skill.Purpose) || !string.IsNullOrEmpty(skill.Description))
        {
            sb.AppendLine("### Purpose");
            sb.AppendLine();
            sb.AppendLine(skill.Purpose.Length > skill.Description.Length ? skill.Purpose : skill.Description);
            sb.AppendLine();
        }

        // Tags
        if (skill.Tags.Count > 0)
        {
            sb.AppendLine("### Tags");
            sb.AppendLine();
            sb.AppendLine(string.Join(", ", skill.Tags.Select(t => $"`{t}`")));
            sb.AppendLine();
        }

        // Best Practices
        if (!string.IsNullOrEmpty(skill.BestPractices))
        {
            sb.AppendLine("### Best Practices");
            sb.AppendLine();
            sb.AppendLine(skill.BestPractices);
            sb.AppendLine();
        }

        // Troubleshooting
        if (!string.IsNullOrEmpty(skill.Troubleshooting))
        {
            sb.AppendLine("### Troubleshooting");
            sb.AppendLine();
            sb.AppendLine(skill.Troubleshooting);
            sb.AppendLine();
        }

        // Relevance Reasons
        if (skill.RelevanceReasons.Count > 0)
        {
            sb.AppendLine("### Why This Skill Is Relevant");
            sb.AppendLine();
            foreach (var reason in skill.RelevanceReasons)
            {
                sb.AppendLine($"- {reason}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    internal static string SanitizeFileName(string name) =>
        Regex.Replace(name.ToLowerInvariant().Replace(' ', '-'), @"[^a-z0-9\-]", "");

    internal static string SanitizeAnchor(string name) =>
        Regex.Replace(name.ToLowerInvariant().Replace(' ', '-'), @"[^a-z0-9\-]", "");

    internal static string EscapeMarkdown(string text) =>
        text.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");

    internal static string GetRelevanceBar(double score) => score switch
    {
        >= 0.8 => "🟢 High",
        >= 0.5 => "🟡 Medium",
        >= 0.2 => "🟠 Low",
        _ => "⚪ Minimal"
    };
}
