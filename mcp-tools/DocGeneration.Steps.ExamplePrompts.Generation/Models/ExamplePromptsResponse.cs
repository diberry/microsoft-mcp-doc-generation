namespace ExamplePromptGeneratorStandalone.Models;

public sealed class ExamplePromptsResponse
{
    public string? ToolName { get; set; }
    public List<string> Prompts { get; set; } = new();
}
