// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace HorizontalArticleGenerator.Models;

/// <summary>
/// AI-generated data for the "overview" namespace fragment call: the service short description and
/// overview paragraph. This is the only fragment whose fields are fatal-if-absent — see
/// <c>HorizontalArticleGenerator.GeneratePerToolAiDataAsync</c>.
/// </summary>
public class NamespaceOverviewFragment
{
    [JsonPropertyName("genai-serviceShortDescription")]
    public string ServiceShortDescription { get; set; } = string.Empty;

    [JsonPropertyName("genai-serviceOverview")]
    public string ServiceOverview { get; set; } = string.Empty;
}

/// <summary>
/// AI-generated data for the "access" namespace fragment call: service-specific prerequisites and
/// required RBAC roles, grounded to the tool list and the authoritative Azure built-in roles page.
/// </summary>
public class NamespaceAccessFragment
{
    [JsonPropertyName("genai-serviceSpecificPrerequisites")]
    public List<Prerequisite> ServiceSpecificPrerequisites { get; set; } = new();

    [JsonPropertyName("genai-requiredRoles")]
    public List<RequiredRole> RequiredRoles { get; set; } = new();
}

/// <summary>
/// AI-generated data for the "best practices" namespace fragment call.
/// </summary>
public class NamespaceBestPracticesFragment
{
    [JsonPropertyName("genai-bestPractices")]
    public List<BestPractice>? BestPractices { get; set; }
}

/// <summary>
/// AI-generated data for the "links" namespace fragment call: the primary service doc link and a
/// short list of additional documentation links.
/// </summary>
public class NamespaceLinksFragment
{
    [JsonPropertyName("genai-serviceDocLink")]
    public string? ServiceDocLink { get; set; }

    [JsonPropertyName("genai-additionalLinks")]
    public List<AdditionalLink> AdditionalLinks { get; set; } = new();
}
