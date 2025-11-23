using System.Text.Json.Serialization;

namespace Azure.Mcp.TextTransformation.Models;

/// <summary>
/// Maps an MCP service name to its brand name and filename.
/// </summary>
public class ServiceMapping
{
    /// <summary>
    /// The MCP server name (e.g., "aks", "storage").
    /// </summary>
    [JsonPropertyName("mcpName")]
    public string McpName { get; set; } = string.Empty;

    /// <summary>
    /// The short name for use in filenames (can be a reference like "$lexicon.acronyms.aks").
    /// </summary>
    [JsonPropertyName("shortName")]
    public string? ShortName { get; set; }

    /// <summary>
    /// The brand name for display (e.g., "Azure Kubernetes Service").
    /// </summary>
    [JsonPropertyName("brandName")]
    public string? BrandName { get; set; }

    /// <summary>
    /// Optional filename override if different from shortName.
    /// </summary>
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    /// <summary>
    /// Optional category for grouping services.
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; set; }
}

/// <summary>
/// Maps a parameter name to its natural language representation.
/// </summary>
public class ParameterMapping
{
    /// <summary>
    /// The technical parameter name (e.g., "subscriptionId").
    /// </summary>
    [JsonPropertyName("parameter")]
    public string Parameter { get; set; } = string.Empty;

    /// <summary>
    /// The natural language representation (e.g., "subscription ID").
    /// </summary>
    [JsonPropertyName("display")]
    public string Display { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the parameter.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
