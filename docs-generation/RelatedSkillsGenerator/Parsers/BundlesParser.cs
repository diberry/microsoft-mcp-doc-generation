// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using RelatedSkillsGenerator.Models;
using Shared;

namespace RelatedSkillsGenerator.Parsers;

/// <summary>
/// Parses BUNDLES.md to extract curated skill bundles.
/// Expected format: ## Bundle Name headings with description and skill tables.
/// </summary>
internal static class BundlesParser
{
    private const string BundlesPageUrl = "https://github.com/MicrosoftDocs/Agent-Skills/blob/main/docs/BUNDLES.md";

    private static readonly Regex SkillLinkRegex = new(
        @"\[([^\]]+)\]\((?:\.\.\/)?skills\/([^/)]+)\/?\)",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses a BUNDLES.md file and returns all bundles with their skills.
    /// </summary>
    public static List<BundleData> Parse(string filePath)
    {
        if (!File.Exists(filePath))
        {
            LogFileHelper.WriteDebug($"BUNDLES.md not found: {filePath}");
            return new List<BundleData>();
        }

        var content = File.ReadAllText(filePath);
        return ParseContent(content);
    }

    /// <summary>
    /// Parses BUNDLES.md content and returns all bundles.
    /// </summary>
    internal static List<BundleData> ParseContent(string content)
    {
        var bundles = new List<BundleData>();
        var lines = content.Split('\n');

        string currentBundleName = string.Empty;
        string currentDescription = string.Empty;
        string currentAnchorId = string.Empty;
        var currentSkills = new List<string>();
        bool inTable = false;
        bool headerRowPassed = false;
        bool collectingDescription = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Detect bundle headings (## emoji BundleName)
            if (trimmed.StartsWith("## ") && !trimmed.StartsWith("## 📍"))
            {
                // Save previous bundle if it had skills
                if (!string.IsNullOrEmpty(currentBundleName) && currentSkills.Count > 0)
                {
                    bundles.Add(new BundleData(currentBundleName, currentDescription, currentAnchorId, new List<string>(currentSkills)));
                }

                currentBundleName = StripEmoji(trimmed[3..]).Trim();
                currentAnchorId = GenerateAnchorId(trimmed[3..]);
                currentDescription = string.Empty;
                currentSkills.Clear();
                inTable = false;
                headerRowPassed = false;
                collectingDescription = true;
                continue;
            }

            // Collect description lines (bold text after heading, before table)
            if (collectingDescription && !inTable)
            {
                if (trimmed.StartsWith("**") && trimmed.Contains("**"))
                {
                    // Extract bold description text
                    var boldMatch = Regex.Match(trimmed, @"\*\*(.+?)\*\*(.*)");
                    if (boldMatch.Success)
                    {
                        currentDescription = boldMatch.Groups[1].Value.TrimEnd('.');
                    }
                }
            }

            // Detect table header row
            if (!inTable && trimmed.StartsWith('|') && trimmed.Contains("Skill") && trimmed.Contains("Description"))
            {
                inTable = true;
                collectingDescription = false;
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

                var linkMatch = SkillLinkRegex.Match(trimmed);
                if (linkMatch.Success)
                {
                    currentSkills.Add(linkMatch.Groups[2].Value);
                }
            }
        }

        // Don't forget the last bundle
        if (!string.IsNullOrEmpty(currentBundleName) && currentSkills.Count > 0)
        {
            bundles.Add(new BundleData(currentBundleName, currentDescription, currentAnchorId, new List<string>(currentSkills)));
        }

        LogFileHelper.WriteDebug($"Parsed {bundles.Count} bundles from BUNDLES.md");
        return bundles;
    }

    /// <summary>
    /// Generates a GitHub-compatible anchor ID from a heading.
    /// </summary>
    private static string GenerateAnchorId(string heading)
    {
        // GitHub anchor rules: lowercase, replace spaces with -, strip non-alphanumeric except -
        var anchor = heading.Trim().ToLowerInvariant();
        anchor = Regex.Replace(anchor, @"[^\w\s-]", "");
        anchor = Regex.Replace(anchor, @"\s+", "-");
        anchor = anchor.Trim('-');
        return anchor;
    }

    /// <summary>
    /// Strips leading emoji characters from a string.
    /// </summary>
    private static string StripEmoji(string text)
    {
        return Regex.Replace(text, @"^[\p{So}\p{Cs}\uFE0F\u200D]+\s*", "").Trim();
    }

    /// <summary>
    /// Returns the base URL for linking to a specific bundle anchor.
    /// </summary>
    public static string GetBundleUrl(string anchorId)
    {
        return $"{BundlesPageUrl}#{anchorId}";
    }
}
