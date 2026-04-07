// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HorizontalArticleGenerator.Models;

namespace HorizontalArticleGenerator.Services;

/// <summary>
/// Reads skills relevance JSON produced by Step 5.
/// Uses its own read model to avoid coupling to the SkillsRelevance project.
/// </summary>
public static class SkillsJsonReader
{
    private const double DefaultMinScore = 0.5;

    /// <summary>
    /// Loads skills from the Step 5 JSON output file.
    /// Returns an empty list when the file is missing or malformed (graceful fallback).
    /// Filters to skills with relevance score >= minScore.
    /// </summary>
    public static async Task<List<SkillEntry>> LoadSkillsAsync(
        string outputBasePath,
        string serviceIdentifier,
        double minScore = DefaultMinScore)
    {
        var sanitized = SanitizeFileName(serviceIdentifier);
        var jsonPath = Path.Combine(outputBasePath, "skills-relevance", $"{sanitized}-skills-relevance.json");

        if (!File.Exists(jsonPath))
        {
            return new List<SkillEntry>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(jsonPath);
            var data = JsonSerializer.Deserialize<SkillsJsonFile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data?.Skills == null)
            {
                return new List<SkillEntry>();
            }

            return data.Skills
                .Where(s => s.RelevanceScore >= minScore)
                .OrderByDescending(s => s.RelevanceScore)
                .Select(s => new SkillEntry
                {
                    Name = s.Name,
                    Description = SanitizeForTable(s.Description),
                    SourceUrl = s.SourceUrl,
                    RelevanceScore = s.RelevanceScore
                })
                .ToList();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"  ⚠️ Failed to parse skills JSON for '{serviceIdentifier}': {ex.Message}");
            return new List<SkillEntry>();
        }
    }

    /// <summary>
    /// Sanitizes text for use in a markdown table cell.
    /// Escapes pipes and removes newlines.
    /// </summary>
    internal static string SanitizeForTable(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
    }

    internal static string SanitizeFileName(string name) =>
        Regex.Replace(name.ToLowerInvariant().Replace(' ', '-'), @"[^a-z0-9\-]", "");

    /// <summary>
    /// Read model matching the Step 5 JSON output schema.
    /// </summary>
    internal class SkillsJsonFile
    {
        [JsonPropertyName("serviceName")]
        public string ServiceName { get; set; } = string.Empty;

        [JsonPropertyName("generatedAt")]
        public string GeneratedAt { get; set; } = string.Empty;

        [JsonPropertyName("skills")]
        public List<SkillsJsonFileEntry> Skills { get; set; } = new();
    }

    internal class SkillsJsonFileEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("sourceUrl")]
        public string SourceUrl { get; set; } = string.Empty;

        [JsonPropertyName("relevanceScore")]
        public double RelevanceScore { get; set; }

        [JsonPropertyName("relevanceLevel")]
        public string RelevanceLevel { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();
    }
}
