using System.Text.Json.Serialization;

namespace CSharpGenerator.Models;

/* DEPRECATED: ExamplePromptsResponse model is only used by deprecated ExamplePromptGenerator
 * 
 * This model was used to deserialize AI-generated example prompts from Azure OpenAI.
 * 
 * Replacement: ExamplePromptGeneratorStandalone package has its own version of this model
 * 
 * All references to this model are in commented-out ExamplePromptGenerator code.
 *
public class ExamplePromptsResponse
{
    [JsonPropertyName("toolName")]
    public string? ToolName { get; set; }
    
    [JsonPropertyName("prompts")]
    public List<string> Prompts { get; set; } = new();
}
*/
