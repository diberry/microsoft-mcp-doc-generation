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
    /// Validates example prompts for a tool using the complete tool file.
    /// </summary>
    /// <param name="toolFileContent">Complete tool file content from generated/tools/*.complete.md</param>
    /// <returns>Validation result from LLM</returns>
    public async Task<ValidationResult?> ValidateWithLLMAsync(string toolFileContent)
    {
        if (!IsInitialized())
        {
            return null;
        }

        try
        {
            // Build user prompt with complete tool file
            var userPrompt = _userPromptTemplate.Replace("{TOOL_FILE_CONTENT}", toolFileContent);

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
    public List<string> Issues { get; set; } = new();
}
