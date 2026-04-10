// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace SkillsRelevance.Models;

/// <summary>
/// JSON output DTO for a service's skill relevance report.
/// </summary>
public class SkillRelevanceOutput
{
    [JsonPropertyName("service")]
    public string Service { get; set; } = string.Empty;

    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = string.Empty;

    [JsonPropertyName("sources")]
    public List<SkillSourceEntry> Sources { get; set; } = new();

    [JsonPropertyName("skills")]
    public List<SkillRelevanceEntry> Skills { get; set; } = new();
}

/// <summary>
/// JSON output DTO for an individual skill's relevance data.
/// </summary>
public class SkillRelevanceEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("relevanceScore")]
    public double RelevanceScore { get; set; }

    [JsonPropertyName("relevanceLevel")]
    public string RelevanceLevel { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("lastUpdated")]
    public string? LastUpdated { get; set; }

    [JsonPropertyName("relevanceReasons")]
    public List<string> RelevanceReasons { get; set; } = new();
}

/// <summary>
/// JSON output DTO for a skill source repository.
/// </summary>
public class SkillSourceEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
