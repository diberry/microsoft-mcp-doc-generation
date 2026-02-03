// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ComposedToolGenerator.Models;

/// <summary>
/// Represents a composed tool with actual content replacing placeholders
/// </summary>
public class ComposedToolData
{
    public string FileName { get; set; } = "";
    public string RawContent { get; set; } = "";
    public string ExamplePromptsContent { get; set; } = "";
    public string ParametersContent { get; set; } = "";
    public string AnnotationsContent { get; set; } = "";
}
