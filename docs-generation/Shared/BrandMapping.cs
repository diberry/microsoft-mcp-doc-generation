// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Represents the mapping between brand names, MCP server names, and file names.
/// </summary>
public class BrandMapping
{
    public string BrandName { get; set; } = string.Empty;
    public string McpServerName { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
