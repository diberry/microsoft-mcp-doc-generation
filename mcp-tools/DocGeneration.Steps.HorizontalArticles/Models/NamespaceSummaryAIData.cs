// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace HorizontalArticleGenerator.Models;

/// <summary>
/// AI-generated namespace-level summary data.
/// Returned by the single namespace summary AI call made after all per-tool calls complete.
/// Contains service-level content that does not depend on individual tool details.
/// </summary>
public class NamespaceSummaryAIData
{
    [JsonPropertyName("genai-serviceShortDescription")]
    public string ServiceShortDescription { get; set; } = string.Empty;

    [JsonPropertyName("genai-serviceOverview")]
    public string ServiceOverview { get; set; } = string.Empty;

    [JsonPropertyName("genai-serviceSpecificPrerequisites")]
    public List<Prerequisite> ServiceSpecificPrerequisites { get; set; } = new();

    [JsonPropertyName("genai-requiredRoles")]
    public List<RequiredRole> RequiredRoles { get; set; } = new();

    [JsonPropertyName("genai-bestPractices")]
    public List<BestPractice>? BestPractices { get; set; }

    [JsonPropertyName("genai-serviceDocLink")]
    public string? ServiceDocLink { get; set; }

    [JsonPropertyName("genai-additionalLinks")]
    public List<AdditionalLink> AdditionalLinks { get; set; } = new();
}
