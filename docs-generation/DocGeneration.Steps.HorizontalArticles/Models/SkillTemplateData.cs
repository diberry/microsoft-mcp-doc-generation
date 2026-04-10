// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace HorizontalArticleGenerator.Models;

/// <summary>
/// Minimal DTO for reading skill entries from the skills-relevance JSON produced by Step 5.
/// This is a local copy to avoid coupling HorizontalArticles to the SkillsRelevance project.
/// </summary>
public class SkillRelevanceJsonOutput
{
    [JsonPropertyName("service")]
    public string Service { get; set; } = string.Empty;

    [JsonPropertyName("skills")]
    public List<SkillRelevanceJsonEntry> Skills { get; set; } = new();
}

/// <summary>
/// Individual skill entry from the skills-relevance JSON.
/// </summary>
public class SkillRelevanceJsonEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("relevanceScore")]
    public double RelevanceScore { get; set; }

    [JsonPropertyName("relevanceLevel")]
    public string RelevanceLevel { get; set; } = string.Empty;
}

/// <summary>
/// Template-ready skill data passed to the Handlebars template.
/// </summary>
public class SkillTemplateData
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
}
