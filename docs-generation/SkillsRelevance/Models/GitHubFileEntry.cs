// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace SkillsRelevance.Models;

/// <summary>
/// Represents a file entry returned by the GitHub Contents API.
/// </summary>
public class GitHubFileEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("git_url")]
    public string GitUrl { get; set; } = string.Empty;

    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("encoding")]
    public string? Encoding { get; set; }

    /// <summary>Returns true if this entry is a file (not a directory).</summary>
    public bool IsFile => Type == "file";

    /// <summary>Returns true if this file is a skill file (.md, .yml, .yaml, .json).</summary>
    public bool IsSkillFile =>
        IsFile && (
            Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
            Name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
            Name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
            Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
}
