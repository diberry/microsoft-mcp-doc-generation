// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using RelatedSkillsGenerator.Models;
using Shared;

namespace RelatedSkillsGenerator.Parsers;

/// <summary>
/// Parses CATALOG.md to extract skills organized by category.
/// Expected format: ## Category headings with | Skill | Description | tables.
/// </summary>
internal static class CatalogParser
{
    private const string SkillsBaseUrl = "https://github.com/MicrosoftDocs/Agent-Skills/tree/main/skills";

    private static readonly Regex SkillLinkRegex = new(
        @"\[([^\]]+)\]\((?:\.\.\/)?skills\/([^/)]+)\/?\)",
        RegexOptions.Compiled);

    private static readonly Regex CategoryHeadingRegex = new(
        @"^##\s+\S+\s+(.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Parses a CATALOG.md file and returns all skills with their category.
    /// </summary>
    public static List<CatalogSkill> Parse(string filePath)
    {
        if (!File.Exists(filePath))
        {
            LogFileHelper.WriteDebug($"CATALOG.md not found: {filePath}");
            return new List<CatalogSkill>();
        }

        var content = File.ReadAllText(filePath);
        return ParseContent(content);
    }

    /// <summary>
    /// Parses CATALOG.md content and returns all skills with their category.
    /// </summary>
    internal static List<CatalogSkill> ParseContent(string content)
    {
        var skills = new List<CatalogSkill>();
        var lines = content.Split('\n');

        string currentCategory = string.Empty;
        bool inTable = false;
        bool headerRowPassed = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Detect category headings (## emoji CategoryName)
            if (trimmed.StartsWith("## ") && !trimmed.StartsWith("## 📊 Summary") && !trimmed.StartsWith("## 📖") && !trimmed.StartsWith("## 📍"))
            {
                // Strip emoji prefix to get clean category name
                currentCategory = StripEmoji(trimmed[3..]).Trim();
                inTable = false;
                headerRowPassed = false;
                continue;
            }

            // Detect table header row
            if (!inTable && trimmed.StartsWith('|') && trimmed.Contains("Skill") && trimmed.Contains("Description"))
            {
                inTable = true;
                continue;
            }

            // Skip separator row
            if (inTable && !headerRowPassed && trimmed.StartsWith('|') && trimmed.Contains("---"))
            {
                headerRowPassed = true;
                continue;
            }

            // Parse data rows
            if (inTable && headerRowPassed)
            {
                if (!trimmed.StartsWith('|'))
                {
                    inTable = false;
                    headerRowPassed = false;
                    continue;
                }

                var skill = ParseSkillRow(trimmed, currentCategory);
                if (skill != null)
                {
                    skills.Add(skill);
                }
            }
        }

        LogFileHelper.WriteDebug($"Parsed {skills.Count} skills from CATALOG.md");
        return skills;
    }

    /// <summary>
    /// Parses a single table row to extract skill name, URL, and description.
    /// </summary>
    private static CatalogSkill? ParseSkillRow(string row, string category)
    {
        var cells = row.Split('|', StringSplitOptions.None)
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();

        if (cells.Count < 2) return null;

        var skillCell = cells[0];
        var description = cells[1];

        var linkMatch = SkillLinkRegex.Match(skillCell);
        if (!linkMatch.Success) return null;

        var skillName = linkMatch.Groups[2].Value;
        var skillUrl = $"{SkillsBaseUrl}/{skillName}";

        return new CatalogSkill(skillName, description, category, skillUrl);
    }

    /// <summary>
    /// Strips leading emoji characters from a string.
    /// </summary>
    private static string StripEmoji(string text)
    {
        // Remove common emoji patterns at start: single emoji + space, or emoji + variation selector + space
        return Regex.Replace(text, @"^[\p{So}\p{Cs}\uFE0F\u200D]+\s*", "").Trim();
    }
}
