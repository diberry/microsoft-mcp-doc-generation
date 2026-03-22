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

    // Multi-namespace merge support (AD-011)
    // All three fields are optional — namespaces without them are standalone.

    /// <summary>
    /// Group identifier for namespaces that merge into a single article.
    /// Should match the primary namespace's FileName.
    /// </summary>
    public string? MergeGroup { get; set; }

    /// <summary>
    /// Position within the merge group (1 = first/primary).
    /// </summary>
    public int? MergeOrder { get; set; }

    /// <summary>
    /// Role in the merge group: "primary" (owns frontmatter, overview, related content)
    /// or "secondary" (contributes only tool H2 sections).
    /// </summary>
    public string? MergeRole { get; set; }
}
