using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Models;

public sealed class EnrichedCliOutputDocument
{
    public List<EnrichedCliOutputTool> Results { get; set; } = [];

    public EnrichmentMetadata EnrichmentMetadata { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class EnrichedCliOutputTool
{
    public string Command { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<CliOutputOption> Option { get; set; } = [];

    public string Area { get; set; } = string.Empty;

    public ToolEnrichment Enrichment { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class ToolEnrichment
{
    public bool Matched { get; set; }

    public List<ConditionalParameterGroup> ConditionalGroups { get; set; } = [];

    public Dictionary<string, ParameterEnhancement> ParameterEnhancements { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Examples { get; set; }
}

public sealed record ConditionalParameterGroup
{
    public string Type { get; init; } = string.Empty;

    public List<string> Parameters { get; init; } = [];

    public string Source { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}

public sealed record ParameterEnhancement
{
    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultValue { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ValuePlaceholder { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? AllowedValues { get; init; }
}

public sealed record EnrichmentMetadata
{
    public int TotalTools { get; init; }

    public int MatchedTools { get; init; }

    public int UnmatchedTools { get; init; }

    public int ConditionalGroupsFound { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}
