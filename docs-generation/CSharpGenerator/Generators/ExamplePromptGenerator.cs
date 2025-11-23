using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CSharpGenerator.Models;

namespace CSharpGenerator.Generators;

/// <summary>
/// Generates example prompts for MCP tools using Azure OpenAI
/// </summary>
public class ExamplePromptGenerator
{
    private readonly GenerativeAI.GenerativeAIClient? _openAIClient;
    private readonly string _systemPrompt;
    private readonly string _userPromptTemplate;

    public ExamplePromptGenerator()
    {
        Console.WriteLine("\n┌─────────────────────────────────────────────┐");
        Console.WriteLine("│  Initializing Example Prompt Generator     │");
        Console.WriteLine("└─────────────────────────────────────────────┘");
        try
        {
            // Try to initialize OpenAI client - may fail if credentials not configured
            Console.Write("  Azure OpenAI Client: ");
            _openAIClient = new GenerativeAI.GenerativeAIClient();
            Console.WriteLine("✅ Connected");
            
            // Load prompt templates
            var promptsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "prompts");
            var systemPromptPath = Path.Combine(promptsDir, "system-prompt-example-prompt.txt");
            var userPromptPath = Path.Combine(promptsDir, "user-prompt-example-prompt.txt");
            
            Console.WriteLine($"  Prompt templates dir: {Path.GetFullPath(promptsDir)}");
            Console.WriteLine($"    System prompt: {(File.Exists(systemPromptPath) ? "✅ Found" : "❌ Missing")}");
            Console.WriteLine($"    User prompt:   {(File.Exists(userPromptPath) ? "✅ Found" : "❌ Missing")}");

            if (File.Exists(systemPromptPath) && File.Exists(userPromptPath))
            {
                _systemPrompt = File.ReadAllText(systemPromptPath);
                _userPromptTemplate = File.ReadAllText(userPromptPath);
                Console.WriteLine($"  ✅ Templates loaded (system: {_systemPrompt.Length} chars, user: {_userPromptTemplate.Length} chars)");
            }
            else
            {
                Console.WriteLine($"  ❌ Template files not found at {promptsDir}");
                _openAIClient = null;
                _systemPrompt = string.Empty;
                _userPromptTemplate = string.Empty;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Initialization failed: {ex.Message}");
            _openAIClient = null;
            _systemPrompt = string.Empty;
            _userPromptTemplate = string.Empty;
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Checks if the generator is properly initialized and ready to generate prompts
    /// </summary>
    public bool IsInitialized()
    {
        return _openAIClient != null && 
               !string.IsNullOrEmpty(_systemPrompt) && 
               !string.IsNullOrEmpty(_userPromptTemplate);
    }

    /// <summary>
    /// Generates example prompts for a given tool
    /// </summary>
    /// <param name="tool">The tool to generate example prompts for</param>
    /// <returns>Tuple containing userPrompt, structured response model, and markdown output, or null if generation fails</returns>
    public async Task<(string userPrompt, ExamplePromptsResponse response)?> GenerateAsync(Tool tool)
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
            var responseText = await _openAIClient.GetChatCompletionAsync(_systemPrompt, userPrompt);

            if (string.IsNullOrEmpty(responseText))
            {
                return null;
            }

            // Parse JSON response into structured model
            ExamplePromptsResponse? promptsResponse = null;
            try
            {
                // Extract JSON from response (might be wrapped in markdown code blocks)
                var jsonText = responseText.Trim();
                if (jsonText.StartsWith("```json"))
                {
                    jsonText = jsonText.Substring(7); // Remove ```json
                    var endIndex = jsonText.LastIndexOf("```");
                    if (endIndex > 0)
                        jsonText = jsonText.Substring(0, endIndex);
                }
                else if (jsonText.StartsWith("```"))
                {
                    jsonText = jsonText.Substring(3); // Remove ```
                    var endIndex = jsonText.LastIndexOf("```");
                    if (endIndex > 0)
                        jsonText = jsonText.Substring(0, endIndex);
                }
                jsonText = jsonText.Trim();

                // Parse the JSON - it's a dictionary with tool name as key
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<string>>>(jsonText);
                if (dict != null && dict.Count > 0)
                {
                    var firstEntry = dict.First();
                    promptsResponse = new ExamplePromptsResponse
                    {
                        ToolName = firstEntry.Key,
                        Prompts = firstEntry.Value
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to parse JSON response for '{tool.Name}': {ex.Message}");
                Console.WriteLine($"Raw response: {responseText}");
            }

            if (promptsResponse == null || !promptsResponse.Prompts.Any())
            {
                Console.WriteLine($"Warning: No prompts extracted from response for '{tool.Name}'");
                return null;
            }
            
            return (userPrompt, promptsResponse);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to generate example prompts for tool '{tool.Name}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Generates a single example prompt file for a tool, including Handlebars template processing.
    /// </summary>
    /// <param name="tool">The tool to generate example prompts for</param>
    /// <param name="examplePromptsDir">Directory to write the example prompts file</param>
    /// <param name="annotationFileName">The annotation filename (used to derive example prompts filename)</param>
    /// <param name="version">CLI version for metadata</param>
    /// <param name="templateProcessor">Function to process Handlebars templates</param>
    /// <returns>Tuple of (successCount, failureCount) - either (1,0) or (0,1) or (0,0)</returns>
    public async Task<(int successCount, int failureCount)> GenerateExamplePromptFileAsync(
        Tool tool,
        string examplePromptsDir,
        string annotationFileName,
        string? version,
        Func<string, Dictionary<string, object>, Task<string>> templateProcessor)
    {
        if (!IsInitialized() || string.IsNullOrEmpty(examplePromptsDir))
            return (0, 0);

        Console.WriteLine($"DEBUG: Generating example prompt for {tool.Command ?? tool.Name}");
        try
        {
            var generatedResult = await GenerateAsync(tool);
            Console.WriteLine($"DEBUG: Result HasValue: {generatedResult.HasValue} for {tool.Command ?? tool.Name}");
            
            if (generatedResult.HasValue)
            {
                var (userPrompt, promptsResponse) = generatedResult.Value;
                Console.WriteLine($"DEBUG: Generated {promptsResponse.Prompts.Count} prompts for {tool.Command ?? tool.Name}");
                
                // Use same filename pattern as annotations: {brand-filename}-{tool-family}-{operation}-example-prompts.md
                var exampleFileName = annotationFileName.Replace("-annotations.md", "-example-prompts.md");
                var exampleOutputFile = Path.Combine(examplePromptsDir, exampleFileName);
                
                // Process template and generate file content
                var fileContent = await ProcessExamplePromptsTemplateAsync(
                    tool, 
                    promptsResponse, 
                    userPrompt, 
                    version, 
                    exampleFileName,
                    templateProcessor);
                    
                await File.WriteAllTextAsync(exampleOutputFile, fileContent);
                tool.HasExamplePrompts = true;
                var displayCommand = tool.Command ?? tool.Name ?? "unknown";
                Console.WriteLine($"  ✅ {displayCommand,-50} → {exampleFileName}");
                return (1, 0); // Success
            }
            else
            {
                var displayCommand = tool.Command ?? tool.Name ?? "unknown";
                Console.WriteLine($"  ❌ {displayCommand,-50} (generation returned empty)");
                return (0, 1); // Failure
            }
        }
        catch (Exception exampleEx)
        {
            var displayCommand = tool.Command ?? tool.Name ?? "unknown";
            Console.WriteLine($"  ❌ {displayCommand,-50} (error: {exampleEx.Message})");
            return (0, 1); // Failure
        }
    }

    /// <summary>
    /// Processes the example prompts Handlebars template and generates file content with frontmatter.
    /// </summary>
    private async Task<string> ProcessExamplePromptsTemplateAsync(
        Tool tool,
        ExamplePromptsResponse promptsResponse,
        string userPrompt,
        string? version,
        string exampleFileName,
        Func<string, Dictionary<string, object>, Task<string>> templateProcessor)
    {
        try
        {
            // Extract action verb and resource type from command
            var commandParts = tool.Command?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            var actionVerb = commandParts.Length > 1 ? commandParts[^1] : "manage";
            var resourceType = commandParts.Length > 1 ? string.Join(" ", commandParts[1..^1]) : "resource";

            // Transform prompts into template format with categories
            var examplePrompts = new List<object>();
            for (int i = 0; i < promptsResponse.Prompts.Count; i++)
            {
                var prompt = promptsResponse.Prompts[i];
                // Use a simple category based on the action verb or prompt position
                var category = i == 0 ? $"{char.ToUpper(actionVerb[0])}{actionVerb.Substring(1)} {resourceType}" 
                                      : $"{char.ToUpper(actionVerb[0])}{actionVerb.Substring(1)} example {i + 1}";
                examplePrompts.Add(new { category, prompt });
            }

            // Transform parameters for template
            var parameters = tool.Option?.Select(opt => (object)new
            {
                name = opt.NL_Name ?? opt.Name ?? "unknown",
                required = opt.Required,
                description = opt.Description ?? "No description"
            }).ToList() ?? new List<object>();

            // Prepare template context
            var templateContext = new Dictionary<string, object>
            {
                ["toolName"] = tool.Name ?? "Unknown",
                ["toolCommand"] = $"<!-- azmcp {tool.Command} -->",
                ["actionVerb"] = actionVerb,
                ["resourceType"] = resourceType,
                ["parameters"] = parameters,
                ["examplePrompts"] = examplePrompts
            };

            // Process Handlebars template using the provided processor function
            var templatesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "templates");
            var templateFile = Path.Combine(templatesDir, "example-prompts-template.hbs");
            var templateOutput = await templateProcessor(templateFile, templateContext);

            // Generate frontmatter using utility
            var frontmatter = FrontmatterUtility.GenerateExamplePromptFrontmatter(
                tool.Command ?? "unknown",
                version,
                exampleFileName,
                userPrompt);

            return frontmatter + templateOutput;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing example prompts template for {tool.Command}: {ex.Message}");
            throw;
        }
    }
}
