using System.Text.Json.Serialization;

namespace CSharpGenerator.Models;

public class ExamplePromptsResponse
{
    [JsonPropertyName("toolName")]
    public string? ToolName { get; set; }
    
    [JsonPropertyName("prompts")]
    public List<string> Prompts { get; set; } = new();
}
