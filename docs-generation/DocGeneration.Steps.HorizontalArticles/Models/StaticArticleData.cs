// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CSharpGenerator.Models;

namespace HorizontalArticleGenerator.Models;

/// <summary>
/// Static data extracted from CLI output and configuration files.
/// This data is available before AI generation.
/// </summary>
public class StaticArticleData
{
    /// <summary>
    /// Service brand name from brand-to-server-mapping.json
    /// Example: "Azure Storage", "Azure Kubernetes Service"
    /// </summary>
    public string ServiceBrandName { get; set; } = string.Empty;
    
    /// <summary>
    /// Service identifier (lowercase service area name)
    /// Example: "storage", "aks", "foundry"
    /// </summary>
    public string ServiceIdentifier { get; set; } = string.Empty;
    
    /// <summary>
    /// ISO 8601 timestamp of when this data was generated
    /// </summary>
    public string GeneratedAt { get; set; } = string.Empty;
    
    /// <summary>
    /// MCP CLI version used to generate the data
    /// </summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// Link to the tools reference page for this service
    /// Example: "../tools/storage.md"
    /// </summary>
    public string ToolsReferenceLink { get; set; } = string.Empty;
    
    /// <summary>
    /// List of MCP tools available for this service
    /// </summary>
    public List<HorizontalToolSummary> Tools { get; set; } = new();
}

/// <summary>
/// Summary information about a single MCP tool for horizontal articles
/// </summary>
public class HorizontalToolSummary
{
    /// <summary>
    /// Full command name
    /// Example: "storage account create"
    /// </summary>
    public string Command { get; set; } = string.Empty;
    
    /// <summary>
    /// Full tool description from CLI output
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Count of non-common parameters (excludes subscription-id, resource-group, etc.)
    /// </summary>
    public int ParameterCount { get; set; }
    
    /// <summary>
    /// Link to parameter reference documentation
    /// Example: "../parameters/storage-account-create-parameters.md"
    /// </summary>
    public string MoreInfoLink { get; set; } = string.Empty;
    
    /// <summary>
    /// Tool metadata (destructive, read-only, requires secrets)
    /// </summary>
    public Dictionary<string, MetadataValue> Metadata { get; set; } = new();
}
