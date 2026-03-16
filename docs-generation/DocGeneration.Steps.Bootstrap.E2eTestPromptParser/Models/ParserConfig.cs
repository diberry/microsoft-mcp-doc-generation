using System.Text.Json.Serialization;

namespace E2eTestPromptParser.Models;

/// <summary>
/// Configuration for the remote e2eTestPrompts.md source file.
/// </summary>
public sealed class ParserConfig
{
    /// <summary>
    /// The remote URL to download the markdown file from.
    /// </summary>
    [JsonPropertyName("remoteUrl")]
    public string RemoteUrl { get; set; } = string.Empty;

    /// <summary>
    /// The local filename to save the downloaded file as.
    /// </summary>
    [JsonPropertyName("localFileName")]
    public string LocalFileName { get; set; } = "e2eTestPrompts.md";
}
