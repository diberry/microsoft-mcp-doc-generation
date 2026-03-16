using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Models;

public sealed class CliOutputDocument
{
    public List<CliOutputTool> Results { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class CliOutputTool
{
    public string Command { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<CliOutputOption> Option { get; set; } = [];

    public string Area { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class CliOutputOption
{
    public string Name { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
