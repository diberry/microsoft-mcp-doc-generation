// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ToolMetadataExtractor.Models;

/// <summary>
/// Represents extracted metadata about a tool with its name and source
/// </summary>
public class ToolMetadataInfo
{
    /// <summary>
    /// Gets or sets the tool path, which includes the namespace and name
    /// </summary>
    public string ToolPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the source file where the tool was found
    /// </summary>
    public string SourceFile { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the metadata properties for this tool
    /// </summary>
    public Dictionary<string, bool> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the title of the tool
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the description of the tool
    /// </summary>
    public string? Description { get; set; }
}