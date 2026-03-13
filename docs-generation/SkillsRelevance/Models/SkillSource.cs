// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SkillsRelevance.Models;

/// <summary>
/// Configuration for a GitHub repository containing skills.
/// </summary>
public class SkillSource
{
    /// <summary>GitHub repository owner.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>GitHub repository name.</summary>
    public string Repo { get; set; } = string.Empty;

    /// <summary>Path within the repository to the skills directory.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Human-readable display name for this source.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets the GitHub API URL for listing contents of the skills path.</summary>
    public string GetContentsApiUrl() =>
        string.IsNullOrEmpty(Path)
            ? $"https://api.github.com/repos/{Owner}/{Repo}/contents"
            : $"https://api.github.com/repos/{Owner}/{Repo}/contents/{Path}";

    /// <summary>Gets the HTML URL for viewing the skills directory on GitHub.</summary>
    public string GetHtmlUrl() =>
        string.IsNullOrEmpty(Path)
            ? $"https://github.com/{Owner}/{Repo}"
            : $"https://github.com/{Owner}/{Repo}/tree/main/{Path}";

    /// <summary>Predefined set of skill sources for Azure MCP documentation.</summary>
    public static IReadOnlyList<SkillSource> Defaults => new List<SkillSource>
    {
        new() { Owner = "github", Repo = "awesome-copilot", Path = "skills", DisplayName = "GitHub Awesome Copilot" },
        new() { Owner = "microsoft", Repo = "skills", Path = "", DisplayName = "Microsoft Skills" },
        new() { Owner = "microsoft", Repo = "GitHub-Copilot-for-Azure", Path = "plugin/skills", DisplayName = "GitHub Copilot for Azure" }
    };
}
