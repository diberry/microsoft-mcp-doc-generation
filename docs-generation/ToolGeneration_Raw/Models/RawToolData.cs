// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ToolGeneration_Raw.Models;

/// <summary>
/// Represents a raw tool file with placeholders
/// </summary>
public class RawToolData
{
    public string? ToolName { get; set; }
    public string? Command { get; set; }
    public string? Description { get; set; }
    public string? FileName { get; set; }
    public string? GeneratedDate { get; set; }
    public string? McpCliVersion { get; set; }
    public string? ExamplePromptsPlaceholder { get; set; }
    public string? ParametersPlaceholder { get; set; }
    public string? AnnotationsPlaceholder { get; set; }
}
