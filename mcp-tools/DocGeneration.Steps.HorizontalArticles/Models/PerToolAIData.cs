// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace HorizontalArticleGenerator.Models;

/// <summary>
/// AI-generated data for a single MCP tool.
/// Returned by the per-tool AI call (one call per tool, not one per namespace).
/// </summary>
public class PerToolAIData
{
    /// <summary>
    /// 10-15 word description of this tool. Maps to tools[].genai-shortDescription.
    /// </summary>
    [JsonPropertyName("genai-shortDescription")]
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>
    /// A usage scenario for this specific tool. Aggregated into AIGeneratedArticleData.Scenarios.
    /// </summary>
    [JsonPropertyName("genai-scenario")]
    public Scenario? Scenario { get; set; }

    /// <summary>
    /// Action phrase describing this tool's capability. Aggregated into AIGeneratedArticleData.Capabilities.
    /// </summary>
    [JsonPropertyName("genai-capability")]
    public string Capability { get; set; } = string.Empty;

    /// <summary>
    /// The command this data was generated for. Set by the caller, not deserialized from AI response.
    /// </summary>
    [JsonIgnore]
    public string Command { get; set; } = string.Empty;
}
