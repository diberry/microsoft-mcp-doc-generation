// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using GenerativeAI;

namespace ExamplePromptValidator;

/// <summary>
/// Validates generated example prompts using LLM to ensure they contain required tool parameters.
/// Uses the GenerativeAI package to perform rich, context-aware validation.
/// </summary>
public class PromptValidator
{
    private readonly GenerativeAIClient? _aiClient;
    private readonly string _systemPrompt;
    private readonly string _userPromptTemplate;

    public PromptValidator()
    {
        try
        {
            _aiClient = new GenerativeAIClient();
            
            // Load validation prompts
            var promptsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "prompts");
            var systemPromptPath = Path.Combine(promptsDir, "system-prompt-validation.txt");
            var userPromptPath = Path.Combine(promptsDir, "user-prompt-validation.txt");
            
            if (File.Exists(systemPromptPath) && File.Exists(userPromptPath))
            {
                _systemPrompt = File.ReadAllText(systemPromptPath);
                _userPromptTemplate = File.ReadAllText(userPromptPath);
            }
            else
            {
                Console.WriteLine($"⚠️  Validation prompt files not found at {promptsDir}");
                _aiClient = null;
                _systemPrompt = string.Empty;
                _userPromptTemplate = string.Empty;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Failed to initialize PromptValidator: {ex.Message}");
            _aiClient = null;
            _systemPrompt = string.Empty;
            _userPromptTemplate = string.Empty;
        }
    }

    /// <summary>
    /// Checks if the validator is properly initialized and ready to validate.
    /// </summary>
    public bool IsInitialized()
    {
        return _aiClient != null && 
               !string.IsNullOrEmpty(_systemPrompt) && 
               !string.IsNullOrEmpty(_userPromptTemplate);
    }

    /// <summary>
    /// Validates example prompts for a tool using the complete tool context.
    /// </summary>
    /// <param name="toolContent">Complete tool documentation content from generated/tools/</param>
    /// <param name="toolName">Name of the tool</param>
    /// <param name="toolCommand">Command for the tool</param>
    /// <param name="toolDescription">Description of the tool</param>
    /// <param name="examplePromptsContent">Generated example prompts content</param>
    /// <returns>Validation result from LLM</returns>
    public async Task<ValidationResult?> ValidateWithLLMAsync(
        string toolContent,
        string toolName,
        string toolCommand,
        string toolDescription,
        string examplePromptsContent)
    {
        if (!IsInitialized())
        {
            return null;
        }

        try
        {
            // Build user prompt with full tool context
            var userPrompt = _userPromptTemplate
                .Replace("{TOOL_NAME}", toolName)
                .Replace("{TOOL_COMMAND}", toolCommand)
                .Replace("{TOOL_DESCRIPTION}", toolDescription)
                .Replace("{PARAMETERS_LIST}", ExtractParametersFromToolContent(toolContent))
                .Replace("{TOOL_METADATA}", ExtractMetadataFromToolContent(toolContent))
                .Replace("{EXAMPLE_PROMPTS}", examplePromptsContent);

            // Call LLM for validation
            var response = await _aiClient!.GetChatCompletionAsync(_systemPrompt, userPrompt);

            if (string.IsNullOrEmpty(response))
            {
                return null;
            }

            // Parse JSON response
            return ParseValidationResponse(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Error during LLM validation: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Extracts parameters section from tool content.
    /// </summary>
    private string ExtractParametersFromToolContent(string toolContent)
    {
        // Extract the parameters section between ## Parameters and the next ## section
        var parametersStart = toolContent.IndexOf("## Parameters");
        if (parametersStart == -1) return "No parameters documented";

        var nextSection = toolContent.IndexOf("##", parametersStart + 13);
        var parametersSection = nextSection == -1 
            ? toolContent.Substring(parametersStart)
            : toolContent.Substring(parametersStart, nextSection - parametersStart);

        return parametersSection.Trim();
    }

    /// <summary>
    /// Extracts metadata section from tool content.
    /// </summary>
    private string ExtractMetadataFromToolContent(string toolContent)
    {
        // Extract the metadata/annotations section
        var metadataStart = toolContent.IndexOf("## Tool Metadata");
        if (metadataStart == -1)
        {
            metadataStart = toolContent.IndexOf("## Annotations");
        }
        
        if (metadataStart == -1) return "No metadata available";

        var nextSection = toolContent.IndexOf("##", metadataStart + 13);
        var metadataSection = nextSection == -1 
            ? toolContent.Substring(metadataStart)
            : toolContent.Substring(metadataStart, nextSection - metadataStart);

        return metadataSection.Trim();
    }

    /// <summary>
    /// Parses the JSON validation response from the LLM.
    /// </summary>
    private ValidationResult? ParseValidationResponse(string response)
    {
        try
        {
            // Clean the response - remove markdown code blocks if present
            var jsonText = response.Trim();
            if (jsonText.StartsWith("```json"))
            {
                jsonText = jsonText.Substring(7);
                var endIndex = jsonText.LastIndexOf("```");
                if (endIndex > 0)
                    jsonText = jsonText.Substring(0, endIndex);
            }
            else if (jsonText.StartsWith("```"))
            {
                jsonText = jsonText.Substring(3);
                var endIndex = jsonText.LastIndexOf("```");
                if (endIndex > 0)
                    jsonText = jsonText.Substring(0, endIndex);
            }
            jsonText = jsonText.Trim();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<ValidationResult>(jsonText, options);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Failed to parse validation response: {ex.Message}");
            Console.WriteLine($"Response: {response}");
            return null;
        }
    }
}

/// <summary>
/// Result of validating example prompts using LLM.
/// </summary>
public class ValidationResult
{
    public string ToolName { get; set; } = "";
    public List<string> RequiredParameters { get; set; } = new();
    public int TotalPrompts { get; set; }
    public int ValidPrompts { get; set; }
    public int InvalidPrompts { get; set; }
    public bool IsValid { get; set; }
    public List<PromptValidation> Validation { get; set; } = new();
    public string Summary { get; set; } = "";
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Validation details for a single prompt.
/// </summary>
public class PromptValidation
{
    public string Prompt { get; set; } = "";
    public bool IsValid { get; set; }
    public List<string> MissingParameters { get; set; } = new();
    public List<string> FoundParameters { get; set; } = new();
}
