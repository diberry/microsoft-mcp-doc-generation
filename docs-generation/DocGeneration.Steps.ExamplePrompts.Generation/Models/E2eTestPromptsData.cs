using System.Text.Json.Serialization;

namespace ExamplePromptGeneratorStandalone.Models;

/// <summary>
/// Deserialization models for parsed.json produced by E2eTestPromptParser.
/// Lightweight DTOs — no dependency on the parser package.
/// </summary>
public sealed class E2eTestPromptsData
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("totalSections")]
    public int TotalSections { get; set; }

    [JsonPropertyName("totalTools")]
    public int TotalTools { get; set; }

    [JsonPropertyName("totalPrompts")]
    public int TotalPrompts { get; set; }

    [JsonPropertyName("sections")]
    public List<E2eSection> Sections { get; set; } = [];
}

public sealed class E2eSection
{
    [JsonPropertyName("heading")]
    public string Heading { get; set; } = string.Empty;

    [JsonPropertyName("toolCount")]
    public int ToolCount { get; set; }

    [JsonPropertyName("promptCount")]
    public int PromptCount { get; set; }

    [JsonPropertyName("tools")]
    public List<E2eToolEntry> Tools { get; set; } = [];
}

public sealed class E2eToolEntry
{
    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = string.Empty;

    [JsonPropertyName("testPrompts")]
    public List<string> TestPrompts { get; set; } = [];
}

/// <summary>
/// Provides fast lookup from tool command (space-separated) to e2e reference prompts.
/// </summary>
public static class E2eTestPromptsLookup
{
    /// <summary>
    /// Builds a dictionary keyed by tool command (spaces, e.g. "advisor recommendation list")
    /// from the parsed e2e data (underscores, e.g. "advisor_recommendation_list").
    /// </summary>
    public static Dictionary<string, List<string>> BuildLookup(E2eTestPromptsData data)
    {
        var lookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in data.Sections)
        {
            foreach (var tool in section.Tools)
            {
                // e2e uses underscores: "advisor_recommendation_list"
                // CLI uses spaces: "advisor recommendation list"
                var command = tool.ToolName.Replace('_', ' ');
                if (!lookup.ContainsKey(command))
                {
                    lookup[command] = tool.TestPrompts;
                }
            }
        }

        return lookup;
    }
}
