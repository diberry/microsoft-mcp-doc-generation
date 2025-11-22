using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpGenerator;

/// <summary>
/// Generates example prompts for MCP tools using Azure OpenAI
/// </summary>
public class ExamplePromptGenerator
{
    private readonly AzureOpenAIClient.AzureOpenAIClient? _openAIClient;
    private readonly string _systemPrompt;
    private readonly string _userPromptTemplate;

    public ExamplePromptGenerator()
    {
        Console.WriteLine("Initializing ExamplePromptGenerator...");
        try
        {
            // Try to initialize OpenAI client - may fail if credentials not configured
            Console.WriteLine("Creating AzureOpenAIClient...");
            _openAIClient = new AzureOpenAIClient.AzureOpenAIClient();
            Console.WriteLine("AzureOpenAIClient created successfully");
            
            // Load prompt templates
            var promptsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "prompts");
            var systemPromptPath = Path.Combine(promptsDir, "system-prompt-example-prompt.txt");
            var userPromptPath = Path.Combine(promptsDir, "user-prompt-example-prompt.txt");
            
            Console.WriteLine($"Looking for prompt templates in: {Path.GetFullPath(promptsDir)}");
            Console.WriteLine($"System prompt exists: {File.Exists(systemPromptPath)}");
            Console.WriteLine($"User prompt exists: {File.Exists(userPromptPath)}");

            if (File.Exists(systemPromptPath) && File.Exists(userPromptPath))
            {
                _systemPrompt = File.ReadAllText(systemPromptPath);
                _userPromptTemplate = File.ReadAllText(userPromptPath);
                Console.WriteLine($"Loaded prompt templates (system: {_systemPrompt.Length} chars, user: {_userPromptTemplate.Length} chars)");
            }
            else
            {
                Console.WriteLine($"Warning: Prompt template files not found at {promptsDir}");
                _openAIClient = null;
                _systemPrompt = string.Empty;
                _userPromptTemplate = string.Empty;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to initialize ExamplePromptGenerator: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            _openAIClient = null;
            _systemPrompt = string.Empty;
            _userPromptTemplate = string.Empty;
        }
    }

    /// <summary>
    /// Generates example prompts for a given tool
    /// </summary>
    /// <param name="tool">The tool to generate example prompts for</param>
    /// <returns>Generated example prompts as markdown string, or null if generation fails</returns>
    public async Task<string?> GenerateAsync(Tool tool)
    {
        // Return null if client not initialized
        if (_openAIClient == null || string.IsNullOrEmpty(_systemPrompt) || string.IsNullOrEmpty(_userPromptTemplate))
        {
            return null;
        }

        try
        {
            // Extract action verb and resource type from command
            var commandParts = tool.Command?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            var actionVerb = commandParts.Length > 1 ? commandParts[^1] : "manage"; // Last part is usually the action
            var resourceType = commandParts.Length > 1 ? string.Join(" ", commandParts[1..^1]) : "resource";

            // Build parameters section
            var parametersBuilder = new StringBuilder();
            var requiredParams = tool.Option?.Where(o => o.Required).ToList() ?? new List<Option>();
            var optionalParams = tool.Option?.Where(o => !o.Required).ToList() ?? new List<Option>();

            foreach (var param in requiredParams)
            {
                parametersBuilder.AppendLine($"- {param.Name} (Required): {param.Description ?? "No description"}");
            }
            foreach (var param in optionalParams)
            {
                parametersBuilder.AppendLine($"- {param.Name} (Optional): {param.Description ?? "No description"}");
            }

            // Fill in the user prompt template
            var userPrompt = _userPromptTemplate
                .Replace("{TOOL_NAME}", tool.Name ?? "Unknown Tool")
                .Replace("{TOOL_COMMAND}", tool.Command ?? "unknown command")
                .Replace("{ACTION_VERB}", actionVerb)
                .Replace("{RESOURCE_TYPE}", resourceType)
                .Replace("{{#each PARAMETERS}}\n- {{name}} ({{#if required}}Required{{else}}Optional{{/if}}): {{description}}\n{{/each}}", 
                    parametersBuilder.ToString().TrimEnd());

            // Add the final instruction
            userPrompt += "\nGenerate the prompts now.";

            // Call Azure OpenAI
            var response = await _openAIClient.GetChatCompletionAsync(_systemPrompt, userPrompt);

            if (string.IsNullOrEmpty(response))
            {
                return null;
            }

            // Format the output as markdown with tool context
            var output = new StringBuilder();
            output.AppendLine($"# Example Prompts: {tool.Name}");
            output.AppendLine();
            output.AppendLine($"**Command**: `{tool.Command}`");
            output.AppendLine();
            output.AppendLine($"**Description**: {tool.Description ?? "No description available"}");
            output.AppendLine();
            output.AppendLine("## Example Prompts");
            output.AppendLine();
            output.AppendLine(response);
            
            return output.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to generate example prompts for tool '{tool.Name}': {ex.Message}");
            return null;
        }
    }
}
