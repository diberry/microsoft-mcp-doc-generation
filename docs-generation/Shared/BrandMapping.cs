namespace Shared;

/// <summary>
/// Represents a mapping between Azure service brand names and MCP server names.
/// </summary>
public class BrandMapping
{
    /// <summary>
    /// The official Azure brand name (e.g., "Azure Container Registry").
    /// </summary>
    public string BrandName { get; set; } = string.Empty;

    /// <summary>
    /// The MCP server name/identifier (e.g., "acr").
    /// </summary>
    public string McpServerName { get; set; } = string.Empty;

    /// <summary>
    /// The short/abbreviated name (e.g., "ACR").
    /// </summary>
    public string ShortName { get; set; } = string.Empty;

    /// <summary>
    /// The filename-friendly version of the brand name, lowercase with hyphens (e.g., "azure-container-registry").
    /// </summary>
    public string FileName { get; set; } = string.Empty;
}
