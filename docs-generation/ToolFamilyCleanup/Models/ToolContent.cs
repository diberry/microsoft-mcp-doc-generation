// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ToolFamilyCleanup.Models;

/// <summary>
/// Represents a single tool's content extracted from a complete tool markdown file.
/// Used in multi-phase processing to group tools by family before stitching.
/// </summary>
public class ToolContent
{
    /// <summary>
    /// Human-readable tool name (e.g., "agents connect")
    /// </summary>
    public required string ToolName { get; init; }
    
    /// <summary>
    /// Original filename from ./generated/tools (e.g., "azure-ai-foundry-agents-connect.complete.md")
    /// </summary>
    public required string FileName { get; init; }
    
    /// <summary>
    /// Tool family name derived from filename prefix (e.g., "foundry" from "azure-ai-foundry-...")
    /// </summary>
    public required string FamilyName { get; init; }
    
    /// <summary>
    /// Markdown content of the tool (H2 heading + description + parameters + examples + annotations)
    /// Frontmatter is stripped during reading.
    /// </summary>
    public required string Content { get; init; }
    
    /// <summary>
    /// Original full path to the source file for debugging
    /// </summary>
    public string? SourceFilePath { get; init; }
}
