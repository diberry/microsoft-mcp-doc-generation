namespace ExamplePromptGeneratorStandalone.Models;

public sealed class Tool
{
    public string? Name { get; set; }
    public string? Command { get; set; }
    public string? Description { get; set; }
    public List<Option>? Option { get; set; }
}
