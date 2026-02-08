// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ToolFamilyCleanup.Models;

/// <summary>
/// Represents a complete tool family documentation file assembled from multiple parts.
/// Used in multi-phase processing to combine metadata, tools, and related content.
/// </summary>
public class FamilyContent
{
    /// <summary>
    /// Tool family name (e.g., "foundry", "fileshares")
    /// </summary>
    public required string FamilyName { get; init; }

    /// <summary>
    /// Brand display name (e.g., "Azure AI Foundry") used in metadata
    /// </summary>
    public string? DisplayName { get; set; }
    
    /// <summary>
    /// AI-generated metadata section including:
    /// - YAML frontmatter (title, description, keywords, ms.service, ms.topic, tool_count)
    /// - H1 heading
    /// - Intro paragraph
    /// </summary>
    public required string Metadata { get; set; }
    
    /// <summary>
    /// List of tools belonging to this family, ordered alphabetically
    /// </summary>
    public required List<ToolContent> Tools { get; init; }
    
    /// <summary>
    /// AI-generated related content section (## Related content with links)
    /// </summary>
    public required string RelatedContent { get; set; }
    
    /// <summary>
    /// Number of tools in this family (convenience property)
    /// </summary>
    public int ToolCount => Tools.Count;
    
    /// <summary>
    /// Comma-separated list of tool names for prompts
    /// </summary>
    public string ToolNamesList => string.Join(", ", Tools.Select(t => t.ToolName).OrderBy(n => n));
}
