using System.Text.Json.Serialization;

namespace PipelineRunner.Services;

/// <summary>
/// Represents a single entry from <c>mcp-tools/data/brand-to-server-mapping.json</c>.
/// The file is an array of these entries.
/// </summary>
public sealed record BrandMappingEntry(
    [property: JsonPropertyName("mcpServerName")] string McpServerName,
    [property: JsonPropertyName("brandName")] string BrandName,
    [property: JsonPropertyName("shortName")] string ShortName,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("composition")] string? Composition = null,
    [property: JsonPropertyName("mergeGroup")] string? MergeGroup = null,
    [property: JsonPropertyName("mergeOrder")] int? MergeOrder = null,
    [property: JsonPropertyName("mergeRole")] string? MergeRole = null);
