// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SkillsRelevance.Models;

/// <summary>
/// Represents a GitHub Copilot skill with all metadata extracted from source files.
/// </summary>
public class SkillInfo
{
    /// <summary>Skill name from frontmatter or filename.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Original filename in the source repository.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Human-readable source repository name.</summary>
    public string SourceRepository { get; set; } = string.Empty;

    /// <summary>GitHub HTML URL for viewing the skill file.</summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>Raw content download URL.</summary>
    public string RawContentUrl { get; set; } = string.Empty;

    /// <summary>Skill description or purpose.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Azure services mentioned in or inferred from the skill.</summary>
    public List<string> AzureServices { get; set; } = new();

    /// <summary>Primary goal or purpose of the skill.</summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>Last updated date (from GitHub commit date or frontmatter).</summary>
    public DateTimeOffset? LastUpdated { get; set; }

    /// <summary>Best practices extracted from skill content.</summary>
    public string BestPractices { get; set; } = string.Empty;

    /// <summary>Troubleshooting tips extracted from skill content.</summary>
    public string Troubleshooting { get; set; } = string.Empty;

    /// <summary>Skill category or type (e.g., "workspace", "azure", "devops").</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Tags from skill frontmatter.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Skill author if available.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Skill version if available.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Full raw content of the skill file.</summary>
    public string RawContent { get; set; } = string.Empty;

    /// <summary>Relevance score (0.0 = not relevant, 1.0 = highly relevant).</summary>
    public double RelevanceScore { get; set; }

    /// <summary>Reasons why this skill was considered relevant.</summary>
    public List<string> RelevanceReasons { get; set; } = new();
}
