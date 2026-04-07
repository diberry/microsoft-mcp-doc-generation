// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace SkillsRelevance.Models;

/// <summary>
/// JSON-serializable output for skills relevance data consumed by downstream steps.
/// </summary>
public class SkillsRelevanceJsonOutput
{
    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = string.Empty;

    [JsonPropertyName("skills")]
    public List<SkillJsonEntry> Skills { get; set; } = new();
}

/// <summary>
/// Individual skill entry in the JSON output.
/// </summary>
public class SkillJsonEntry
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

    /// <summary>
    /// Maps a relevance score to a human-readable level.
    /// </summary>
    public static string ScoreToLevel(double score) => score switch
    {
        >= 0.8 => "High",
        >= 0.5 => "Medium",
        >= 0.2 => "Low",
        _ => "Minimal"
    };

    /// <summary>
    /// Creates a SkillJsonEntry from a SkillInfo model.
    /// </summary>
    public static SkillJsonEntry FromSkillInfo(SkillInfo skill) => new()
    {
        Name = skill.Name,
        Source = skill.SourceRepository,
        SourceUrl = skill.SourceUrl,
        RelevanceScore = skill.RelevanceScore,
        RelevanceLevel = ScoreToLevel(skill.RelevanceScore),
        Description = skill.Description,
        Tags = skill.Tags
    };
}
