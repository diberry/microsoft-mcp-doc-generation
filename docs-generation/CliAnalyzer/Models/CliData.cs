using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace CliAnalyzer.Models;

public class CliResponse
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("results")]
    public List<Tool> Results { get; set; } = [];
}

public class Tool
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("option")]
    public List<Parameter> Options { get; set; } = [];

    [JsonPropertyName("metadata")]
    public Dictionary<string, ToolMetadata> Metadata { get; set; } = [];

    public string? Namespace
    {
        get
        {
            var parts = Command.Split(' ');
            return parts.Length > 0 ? parts[0] : null;
        }
    }

    public int RequiredParameterCount => Options.Count(p => p.Required);
    public int OptionalParameterCount => Options.Count(p => !p.Required);
    public int TotalParameterCount => Options.Count;
}

public class Parameter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

public class ToolMetadata
{
    [JsonPropertyName("value")]
    public bool Value { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}
