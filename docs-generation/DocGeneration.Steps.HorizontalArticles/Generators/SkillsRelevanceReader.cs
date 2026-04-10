// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using HorizontalArticleGenerator.Models;

namespace HorizontalArticleGenerator.Generators;

/// <summary>
/// Reads skills-relevance JSON output from Step 5 and filters to relevant skills.
/// This reader is warn-only: it never throws and returns an empty list on any failure.
/// </summary>
public static class SkillsRelevanceReader
{
    private const double MinRelevanceScore = 0.3;

    private static readonly HashSet<string> QualifyingLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "high",
        "medium"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Loads relevant skills for a service from the skills-relevance JSON file.
    /// Returns an empty list if the file doesn't exist, is invalid, or contains no qualifying skills.
    /// </summary>
    public static List<SkillTemplateData> LoadRelevantSkills(string outputBasePath, string serviceIdentifier)
    {
        try
        {
            var jsonPath = Path.Combine(outputBasePath, "skills-relevance", $"{serviceIdentifier}-skills-relevance.json");
            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"  ℹ️  No skills-relevance JSON found for '{serviceIdentifier}' (Step 5 may not have run).");
                return new List<SkillTemplateData>();
            }

            var json = File.ReadAllText(jsonPath);
            var output = JsonSerializer.Deserialize<SkillRelevanceJsonOutput>(json, JsonOptions);
            if (output?.Skills == null || output.Skills.Count == 0)
            {
                return new List<SkillTemplateData>();
            }

            var filtered = output.Skills
                .Where(IsRelevant)
                .Select(s => new SkillTemplateData
                {
                    Name = s.Name,
                    Description = SanitizeForMarkdownTable(s.Description),
                    SourceUrl = s.SourceUrl
                })
                .ToList();

            if (filtered.Count > 0)
            {
                Console.WriteLine($"  ✅ Found {filtered.Count} relevant skill(s) for horizontal article.");
            }

            return filtered;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  Could not load skills-relevance data for '{serviceIdentifier}': {ex.Message}");
            return new List<SkillTemplateData>();
        }
    }

    /// <summary>
    /// Determines if a skill entry qualifies for inclusion in the horizontal article.
    /// A skill qualifies if its relevanceScore > 0.3 OR its relevanceLevel is "high" or "medium".
    /// </summary>
    internal static bool IsRelevant(SkillRelevanceJsonEntry skill) =>
        skill.RelevanceScore > MinRelevanceScore ||
        QualifyingLevels.Contains(skill.RelevanceLevel);

    /// <summary>
    /// Sanitizes a description for safe use inside a markdown table cell.
    /// Escapes pipe characters and collapses newlines to single spaces.
    /// </summary>
    internal static string SanitizeForMarkdownTable(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("\r\n", " ")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("|", "\\|")
            .Trim();
    }
}
