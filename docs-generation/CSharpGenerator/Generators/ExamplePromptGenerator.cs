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
    private readonly string? _serviceSpecificDir;
    private readonly Dictionary<string, string> _serviceInstructionsCache = new();

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
            
            // Check for service-specific instructions directory
            _serviceSpecificDir = Path.Combine(promptsDir, "service-specific");
            if (Directory.Exists(_serviceSpecificDir))
            {
                var instructionFiles = Directory.GetFiles(_serviceSpecificDir, "*-instructions.md");
                Console.WriteLine($"  Service-specific instructions: {instructionFiles.Length} files found");
                foreach (var file in instructionFiles)
                {
                    Console.WriteLine($"    📄 {Path.GetFileName(file)}");
                }
            }
            else
            {
                Console.WriteLine($"  Service-specific instructions: directory not found");
                _serviceSpecificDir = string.Empty;
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
    /// Gets service-specific instructions for a given service area (e.g., "keyvault", "storage")
    /// </summary>
    /// <param name="serviceArea">The service area identifier</param>
    /// <returns>Service-specific instructions or empty string if not found</returns>
    private string GetServiceSpecificInstructions(string serviceArea)
    {
        if (string.IsNullOrEmpty(_serviceSpecificDir) || string.IsNullOrEmpty(serviceArea))
            return string.Empty;

        // Check cache first
        if (_serviceInstructionsCache.TryGetValue(serviceArea, out var cached))
            return cached;

        // Try to load service-specific instructions file
        var instructionsPath = Path.Combine(_serviceSpecificDir, $"{serviceArea}-instructions.md");
        if (File.Exists(instructionsPath))
        {
            var instructions = File.ReadAllText(instructionsPath);
            _serviceInstructionsCache[serviceArea] = instructions;
            Console.WriteLine($"    📋 Loaded service-specific instructions for '{serviceArea}'");
            return instructions;
        }

        // Cache empty result to avoid repeated file system checks
        _serviceInstructionsCache[serviceArea] = string.Empty;
        return string.Empty;
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
            var serviceArea = commandParts.Length > 0 ? commandParts[0].ToLowerInvariant() : string.Empty;
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

            // Build final system prompt with service-specific instructions if available
            var serviceInstructions = GetServiceSpecificInstructions(serviceArea);
            var finalSystemPrompt = _systemPrompt;
            if (!string.IsNullOrEmpty(serviceInstructions))
            {
                Console.WriteLine($"    📋 Applying service-specific instructions for '{serviceArea}'");
                finalSystemPrompt = _systemPrompt + "\n\n" + serviceInstructions;
            }

            // Call Azure OpenAI
            var responseText = await _openAIClient.GetChatCompletionAsync(finalSystemPrompt, userPrompt);

            if (string.IsNullOrEmpty(responseText))
            {
                return null;
            }

            // Parse JSON response into structured model
            ExamplePromptsResponse? promptsResponse = null;
            try
            {
                // Extract JSON from response (might be wrapped in markdown code blocks or have preamble text)
                var jsonText = responseText.Trim();
                
                // Look for JSON code block first
                if (jsonText.Contains("```json"))
                {
                    var startIndex = jsonText.IndexOf("```json") + 7;
                    var endIndex = jsonText.IndexOf("```", startIndex);
                    if (endIndex > startIndex)
                    {
                        jsonText = jsonText.Substring(startIndex, endIndex - startIndex);
                    }
                }
                else if (jsonText.Contains("```"))
                {
                    var startIndex = jsonText.IndexOf("```") + 3;
                    var endIndex = jsonText.LastIndexOf("```");
                    if (endIndex > startIndex)
                    {
                        jsonText = jsonText.Substring(startIndex, endIndex - startIndex);
                    }
                }
                else
                {
                    // No code blocks - look for JSON object by finding first { and last }
                    var firstBrace = jsonText.IndexOf('{');
                    var lastBrace = jsonText.LastIndexOf('}');
                    if (firstBrace >= 0 && lastBrace > firstBrace)
                    {
                        jsonText = jsonText.Substring(firstBrace, lastBrace - firstBrace + 1);
                    }
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
                        // Clean each prompt to fix smart quotes and HTML entities
                        Prompts = firstEntry.Value
                            .Select(p => NaturalLanguageGenerator.TextCleanup.CleanAIGeneratedText(p))
                            .ToList()
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to parse JSON response for '{tool.Name}': {ex.Message}");
                Console.WriteLine($"Raw response (first 500 chars): {responseText.Substring(0, Math.Min(500, responseText.Length))}...");
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
    /// Creates two files:
    /// 1. Input prompt file in prompts-for-example-tool-prompts/ directory (with userPrompt metadata)
    /// 2. Generated example prompts file in example-prompts/ directory (clean template output)
    /// </summary>
    /// <param name="tool">The tool to generate example prompts for</param>
    /// <param name="examplePromptsDir">Directory to write the generated example prompts file</param>
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
                
                // Directory structure:
                // - Input prompts: ../prompts-for-example-tool-prompts/
                // - Generated prompts: ../example-prompts/
                var parentDir = Directory.GetParent(examplePromptsDir)?.FullName ?? examplePromptsDir;
                var inputPromptsDir = Path.Combine(parentDir, "prompts-for-example-tool-prompts");
                Directory.CreateDirectory(inputPromptsDir);
                
                // 1. Save input prompt file (with userPrompt metadata)
                var inputPromptFileName = annotationFileName.Replace("-annotations.md", "-input-prompt.md");
                var inputPromptFile = Path.Combine(inputPromptsDir, inputPromptFileName);
                var inputPromptContent = FrontmatterUtility.GenerateInputPromptFrontmatter(
                    tool.Command ?? "unknown",
                    version,
                    inputPromptFileName,
                    userPrompt);
                await File.WriteAllTextAsync(inputPromptFile, inputPromptContent);
                
                // 2. Save generated example prompts file (clean template output)
                var exampleOutputFile = Path.Combine(examplePromptsDir, exampleFileName);
                var examplePromptsContent = await ProcessExamplePromptsTemplateAsync(
                    tool, 
                    promptsResponse, 
                    version, 
                    templateProcessor);
                await File.WriteAllTextAsync(exampleOutputFile, examplePromptsContent);
                
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
    /// Processes the example prompts Handlebars template and generates clean output with frontmatter.
    /// This produces the final generated example prompts file content.
    /// </summary>
    private async Task<string> ProcessExamplePromptsTemplateAsync(
        Tool tool,
        ExamplePromptsResponse promptsResponse,
        string? version,
        Func<string, Dictionary<string, object>, Task<string>> templateProcessor)
    {
        try
        {
            // Transform prompts into template format
            var examplePrompts = promptsResponse.Prompts.Select(p => new { prompt = p }).ToList();

            // Prepare template context
            var templateContext = new Dictionary<string, object>
            {
                ["version"] = version ?? "unknown",
                ["generatedAt"] = DateTime.UtcNow,
                ["examplePrompts"] = examplePrompts
            };

            // Process Handlebars template using the provided processor function
            var templatesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "templates");
            var templateFile = Path.Combine(templatesDir, "example-prompts-template.hbs");
            var templateOutput = await templateProcessor(templateFile, templateContext);

            return templateOutput;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing example prompts template for {tool.Command}: {ex.Message}");
            throw;
        }
    }
}
