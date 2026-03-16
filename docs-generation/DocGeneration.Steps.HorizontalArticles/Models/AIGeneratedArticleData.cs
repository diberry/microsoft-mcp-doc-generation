// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace HorizontalArticleGenerator.Models;

/// <summary>
/// AI-generated content for horizontal articles.
/// All properties map to genai- prefixed template variables.
/// </summary>
public class AIGeneratedArticleData
{
    [JsonPropertyName("genai-serviceShortDescription")]
    public string ServiceShortDescription { get; set; } = string.Empty;
    
    [JsonPropertyName("genai-serviceOverview")]
    public string ServiceOverview { get; set; } = string.Empty;
    
    [JsonPropertyName("genai-capabilities")]
    public List<string> Capabilities { get; set; } = new();
    
    [JsonPropertyName("genai-serviceSpecificPrerequisites")]
    public List<Prerequisite> ServiceSpecificPrerequisites { get; set; } = new();
    
    public List<ToolWithAIDescription> Tools { get; set; } = new();
    
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

public class Prerequisite
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ToolWithAIDescription
{
    public string Command { get; set; } = string.Empty;
    
    [JsonPropertyName("genai-shortDescription")]
    public string ShortDescription { get; set; } = string.Empty;
    
    public string MoreInfoLink { get; set; } = string.Empty;
}

public class Scenario
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Examples { get; set; } = new();
    public string ExpectedOutcome { get; set; } = string.Empty;
}

public class AIScenario
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Examples { get; set; } = new();
}

public class RequiredRole
{
    public string Name { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
}

public class CommonIssue
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
}

public class BestPractice
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class AdditionalLink
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
