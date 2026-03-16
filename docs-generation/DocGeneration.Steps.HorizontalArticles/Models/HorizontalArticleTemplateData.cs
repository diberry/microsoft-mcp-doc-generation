// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using CSharpGenerator.Models;

namespace HorizontalArticleGenerator.Models;

/// <summary>
/// Combined data model for horizontal article template rendering.
/// Merges static data (from CLI output) with AI-generated content.
/// </summary>
public class HorizontalArticleTemplateData
{
    // ===== Static fields (from CLI output/config) =====
    
    public string ServiceBrandName { get; set; } = string.Empty;
    public string ServiceIdentifier { get; set; } = string.Empty;
    public string GeneratedAt { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ToolsReferenceLink { get; set; } = string.Empty;
    
    // ===== AI-generated fields (genai- prefix in template) =====
    
    [JsonPropertyName("genai-serviceShortDescription")]
    public string ServiceShortDescription { get; set; } = string.Empty;
    
    [JsonPropertyName("genai-serviceOverview")]
    public string ServiceOverview { get; set; } = string.Empty;
    
    [JsonPropertyName("genai-capabilities")]
    public List<string> Capabilities { get; set; } = new();
    
    [JsonPropertyName("genai-serviceSpecificPrerequisites")]
    public List<Prerequisite> ServiceSpecificPrerequisites { get; set; } = new();
    
    // ===== Merged tools (static + AI) =====
    
    public List<MergedTool> Tools { get; set; } = new();
    
    // ===== AI-generated scenario and guidance fields =====
    
    [JsonPropertyName("genai-scenarios")]
    public List<Scenario> Scenarios { get; set; } = new();
    
    [JsonPropertyName("genai-aiSpecificScenarios")]
    public List<AIScenario>? AISpecificScenarios { get; set; }
    
    [JsonPropertyName("genai-requiredRoles")]
    public List<RequiredRole> RequiredRoles { get; set; } = new();
    
    [JsonPropertyName("genai-authenticationNotes")]
    public string? AuthenticationNotes { get; set; }
    
    [JsonPropertyName("genai-commonIssues")]
    public List<CommonIssue>? CommonIssues { get; set; }
    
    [JsonPropertyName("genai-bestPractices")]
    public List<BestPractice>? BestPractices { get; set; }
    
    [JsonPropertyName("genai-serviceDocLink")]
    public string ServiceDocLink { get; set; } = string.Empty;
    
    [JsonPropertyName("genai-additionalLinks")]
    public List<AdditionalLink> AdditionalLinks { get; set; } = new();
}

/// <summary>
/// Tool information combining static data with AI-generated description
/// </summary>
public class MergedTool
{
    /// <summary>
    /// Command name from static data
    /// </summary>
    public string Command { get; set; } = string.Empty;
    
    /// <summary>
    /// Link to parameter reference from static data
    /// </summary>
    public string MoreInfoLink { get; set; } = string.Empty;
    
    /// <summary>
    /// AI-generated short description
    /// </summary>
    [JsonPropertyName("genai-shortDescription")]
    public string ShortDescription { get; set; } = string.Empty;
}
