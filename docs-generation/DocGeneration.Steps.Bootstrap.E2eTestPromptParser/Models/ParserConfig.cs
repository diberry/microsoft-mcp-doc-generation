using System.Text.Json.Serialization;

namespace E2eTestPromptParser.Models;

/// <summary>
/// Configuration for the remote e2eTestPrompts.md source file.
/// </summary>
public sealed class ParserConfig
{
    /// <summary>
    /// The remote URL to download the markdown file from.
    /// Used as the default when no branch override or template is provided.
    /// </summary>
    [JsonPropertyName("remoteUrl")]
    public string RemoteUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional URL template with {branch} placeholder for branch-parameterized fetching.
    /// </summary>
    [JsonPropertyName("remoteUrlTemplate")]
    public string? RemoteUrlTemplate { get; set; }

    /// <summary>
    /// The local filename to save the downloaded file as.
    /// </summary>
    [JsonPropertyName("localFileName")]
    public string LocalFileName { get; set; } = "e2eTestPrompts.md";

    /// <summary>
    /// Returns the effective URL. If a branch override is given and a template exists,
    /// the template is used with {branch} substituted. Otherwise falls back to <see cref="RemoteUrl"/>.
    /// </summary>
    public string GetEffectiveUrl(string? branchOverride)
    {
        if (!string.IsNullOrWhiteSpace(branchOverride) && !string.IsNullOrWhiteSpace(RemoteUrlTemplate))
        {
            return RemoteUrlTemplate.Replace("{branch}", branchOverride.Trim());
        }

        return RemoteUrl;
    }
}
