// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace RawToolGenerator.Models;

/// <summary>
/// Represents CLI output data for raw tool generation
/// </summary>
public class CliOutput
{
    public List<Tool> Results { get; set; } = new();
}

/// <summary>
/// Represents a single tool from CLI output
/// </summary>
public class Tool
{
    public string? Name { get; set; }
    public string? Command { get; set; }
    public string? Description { get; set; }
    public string? SourceFile { get; set; }
    public List<Option>? Option { get; set; }
    public string? Area { get; set; }
}

/// <summary>
/// Represents a tool parameter/option
/// </summary>
public class Option
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool Required { get; set; }
}
