using System.Text;
using System.Text.Json;
using ExamplePromptGeneratorStandalone.Models;
using GenerativeAI;

namespace ExamplePromptGeneratorStandalone.Generators;

/// <summary>
/// Standalone generator for example prompts using Azure OpenAI.
/// Uses embedded templates and prompts from package folder.
/// </summary>
public sealed class ExamplePromptGenerator
{
    private readonly GenerativeAIClient? _openAIClient;
    private readonly string _systemPrompt;
    private readonly string _userPromptTemplate;

    public ExamplePromptGenerator()
    {
        Console.WriteLine("\n┌─────────────────────────────────────────────┐");
        Console.WriteLine("│  Initializing Standalone Example Generator  │");
        Console.WriteLine("└─────────────────────────────────────────────┘\n");

        try
        {
            _openAIClient = new GenerativeAIClient();
            Console.WriteLine("  ✅ Azure OpenAI client initialized");

            // Use embedded prompts from package folder
            var promptsDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "prompts");
            var systemPromptPath = Path.Combine(promptsDir, "system-prompt-example-prompt.txt");
            var userPromptPath = Path.Combine(promptsDir, "user-prompt-example-prompt.txt");

            if (!File.Exists(systemPromptPath) || !File.Exists(userPromptPath))
            {
                throw new FileNotFoundException($"Prompt templates not found in {promptsDir}");
            }

            _systemPrompt = File.ReadAllText(systemPromptPath);
            _userPromptTemplate = File.ReadAllText(userPromptPath);
            Console.WriteLine("  ✅ Prompt templates loaded\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Initialization failed: {ex.Message}\n");
            _openAIClient = null;
            _systemPrompt = string.Empty;
            _userPromptTemplate = string.Empty;
        }
    }

    public bool IsInitialized => _openAIClient != null && !string.IsNullOrEmpty(_systemPrompt);

    /// <summary>
    /// Generates example prompts for a tool using Azure OpenAI.
    /// When e2e reference prompts are provided, they are injected as few-shot examples
    /// and the prompt count matches the e2e count (the tool authority's intent).
    /// </summary>
    public async Task<(string userPrompt, ExamplePromptsResponse? response, string rawResponse)?> GenerateAsync(
        Tool tool, List<string>? referencePrompts = null)
    {
        if (!IsInitialized || tool == null)
            return null;

        try
        {
            // Parse command for service area
            var commandParts = (tool.Command ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var serviceArea = commandParts.Length > 0 ? commandParts[0].ToLowerInvariant() : string.Empty;
            var actionVerb = commandParts.Length > 1 ? commandParts[^1] : "manage";
            var resourceType = commandParts.Length > 1 ? string.Join(" ", commandParts[1..^1]) : "resource";

            // Determine prompt count: match e2e count or default to 5
            var promptCount = referencePrompts?.Count ?? 5;

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

            // Fill user prompt template - use regex to replace handlebars block
            var userPrompt = _userPromptTemplate
                .Replace("{TOOL_NAME}", tool.Name ?? "Unknown")
                .Replace("{TOOL_COMMAND}", tool.Command ?? "unknown")
                .Replace("{TOOL_DESCRIPTION}", tool.Description ?? "No description available")
                .Replace("{ACTION_VERB}", actionVerb)
                .Replace("{RESOURCE_TYPE}", resourceType)
                .Replace("{PROMPT_COUNT}", promptCount.ToString());
            
            // Replace the handlebars {{#each PARAMETERS}} block with actual parameters
            // Use regex to handle different line endings
            var parametersPattern = @"\{\{#each\s+PARAMETERS\}\}\s*\n.*?\{\{/each\}\}";
            userPrompt = System.Text.RegularExpressions.Regex.Replace(
                userPrompt, 
                parametersPattern, 
                parametersBuilder.ToString().TrimEnd(),
                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            // Inject e2e reference prompts as few-shot examples
            if (referencePrompts != null && referencePrompts.Count > 0)
            {
                // Detect style patterns from reference prompts
                var hasTrailingPunctuation = referencePrompts.Any(p => p.EndsWith('.') || p.EndsWith('?') || p.EndsWith('!'));
                var usesAngleBrackets = referencePrompts.Any(p => p.Contains('<') && p.Contains('>'));

                var refBuilder = new StringBuilder();
                refBuilder.AppendLine();
                refBuilder.AppendLine("## Reference prompts from the tool authority (e2e tests)");
                refBuilder.AppendLine();
                refBuilder.AppendLine($"The following {referencePrompts.Count} prompts were written by the tool's developers as end-to-end test prompts.");
                refBuilder.AppendLine("These are the AUTHORITATIVE source for style, phrasing, complexity, and parameter usage.");
                refBuilder.AppendLine($"Generate exactly {promptCount} prompts (matching the reference count).");
                refBuilder.AppendLine();
                refBuilder.AppendLine("**MANDATORY STYLE RULES derived from these references:**");
                if (!hasTrailingPunctuation)
                {
                    refBuilder.AppendLine("- NO trailing punctuation (no periods, question marks, or exclamation marks at the end)");
                }
                if (usesAngleBrackets)
                {
                    refBuilder.AppendLine("- Use <angle-bracket> placeholders for parameter values (NOT quoted fake values)");
                }
                refBuilder.AppendLine("- Match the brevity and directness of the references — do NOT over-embellish");
                refBuilder.AppendLine("- Use the same terminology (e.g., if references say 'Advisor' not 'Azure Advisor', follow that)");
                refBuilder.AppendLine("- Do NOT add parameters the references deliberately omit");
                refBuilder.AppendLine();
                refBuilder.AppendLine("Reference prompts:");
                for (int i = 0; i < referencePrompts.Count; i++)
                {
                    refBuilder.AppendLine($"{i + 1}. \"{referencePrompts[i]}\"");
                }
                userPrompt += refBuilder.ToString();
            }

            userPrompt += "\n\nGenerate the prompts now.";

            // Call Azure OpenAI
            var responseText = await _openAIClient!.GetChatCompletionAsync(_systemPrompt, userPrompt);

            if (string.IsNullOrEmpty(responseText))
                return null;

            // Parse JSON response (may fail)
            ExamplePromptsResponse? promptsResponse = ParseJsonResponse(responseText);

            if (promptsResponse != null && promptsResponse.Prompts.Any())
            {
                // Clean AI-generated text
                promptsResponse.Prompts = promptsResponse.Prompts
                    .Select(p => CleanAIGeneratedText(p))
                    .ToList();
            }

            return (userPrompt, promptsResponse, responseText);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ❌ Generation failed for '{tool.Command}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses JSON response from LLM, extracting first dictionary entry's prompts.
    /// Uses ExtractJsonFromLLMResponse to isolate the JSON from any preamble/reasoning.
    /// </summary>
    private static ExamplePromptsResponse? ParseJsonResponse(string response)
    {
        try
        {
            // STEP 1: Extract pure JSON from LLM response (removes preamble, code blocks, etc.)
            var jsonText = ExtractJsonFromLLMResponse(response);
            
            if (string.IsNullOrEmpty(jsonText))
            {
                Console.WriteLine("  ⚠️  No JSON found in LLM response");
                return null;
            }

            // STEP 2: Deserialize the JSON (allow trailing commas from LLM output)
            var jsonOptions = new JsonSerializerOptions
            {
                AllowTrailingCommas = true
            };

            var dict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(jsonText, jsonOptions);

            // STEP 3: Extract first entry as the tool's example prompts
            if (dict != null && dict.Count > 0)
            {
                var firstEntry = dict.First();
                return new ExamplePromptsResponse
                {
                    ToolName = firstEntry.Key,
                    Prompts = firstEntry.Value
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠️  JSON parse failed: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// ═══════════════════════════════════════════════════════════════
    /// DEDICATED FUNCTION: Extract JSON from LLM Response
    /// ═══════════════════════════════════════════════════════════════
    /// 
    /// Purpose: LLMs often return responses with preamble text, reasoning steps,
    /// verification checklists, and other content BEFORE the actual JSON object.
    /// This function isolates ONLY the JSON object from that response.
    /// 
    /// Strategies (in order of priority):
    /// 1. Look for ```json code block (most explicit)
    /// 2. Look for the LAST ``` code block (likely the final answer)
    /// 3. Find the last complete JSON object (using brace matching)
    /// 
    /// Returns: Pure JSON string, or empty if no JSON found
    /// ═══════════════════════════════════════════════════════════════
    /// </summary>
    private static string ExtractJsonFromLLMResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
            return string.Empty;

        var text = response.Trim();

        // ───────────────────────────────────────────────────────────
        // STRATEGY 1: Look for ```json code block
        // ───────────────────────────────────────────────────────────
        if (text.Contains("```json"))
        {
            var start = text.IndexOf("```json") + 7;
            var end = text.IndexOf("```", start);
            if (end > start)
            {
                var extracted = text.Substring(start, end - start).Trim();
                Console.WriteLine("  📝 JSON extracted from ```json block");
                return extracted;
            }
        }

        // ───────────────────────────────────────────────────────────
        // STRATEGY 2: Find the LAST ``` code block
        // (LLM often puts final answer at the end)
        // ───────────────────────────────────────────────────────────
        if (text.Contains("```"))
        {
            var lastClosingTick = text.LastIndexOf("```");
            if (lastClosingTick > 0)
            {
                // Find the opening ``` before this closing one
                var lastOpeningTick = text.LastIndexOf("```", lastClosingTick - 1);
                if (lastOpeningTick >= 0)
                {
                    var blockContent = text.Substring(lastOpeningTick + 3, lastClosingTick - lastOpeningTick - 3).Trim();
                    // Verify it starts with { (looks like JSON)
                    if (blockContent.StartsWith("{"))
                    {
                        Console.WriteLine("  📝 JSON extracted from last ``` block");
                        return blockContent;
                    }
                }
            }
        }

        // ───────────────────────────────────────────────────────────
        // STRATEGY 3: Find last complete JSON object
        // Use brace matching to find the outermost { ... }
        // ───────────────────────────────────────────────────────────
        var lastClosingBrace = text.LastIndexOf('}');
        if (lastClosingBrace >= 0)
        {
            // Search backwards to find the matching opening brace
            int braceCount = 1;
            for (int i = lastClosingBrace - 1; i >= 0; i--)
            {
                if (text[i] == '}') braceCount++;
                else if (text[i] == '{') braceCount--;
                
                if (braceCount == 0)
                {
                    var extracted = text.Substring(i, lastClosingBrace - i + 1).Trim();
                    Console.WriteLine("  📝 JSON extracted using brace matching");
                    return extracted;
                }
            }
        }

        Console.WriteLine("  ⚠️  No JSON structure found in response");
        return string.Empty;
    }

    /// <summary>
    /// Cleans AI-generated text: smart quotes → straight quotes, HTML entities → plain text.
    /// </summary>
    private static string CleanAIGeneratedText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        text = text.Replace('\u2018', '\'');
        text = text.Replace('\u2019', '\'');
        text = text.Replace('\u201C', '"');
        text = text.Replace('\u201D', '"');
        text = text.Replace("&quot;", "\"");
        text = text.Replace("&#34;", "\"");
        text = text.Replace("&apos;", "'");
        text = text.Replace("&#39;", "'");
        text = text.Replace("&amp;", "&");
        text = text.Replace("&lt;", "<");
        text = text.Replace("&gt;", ">");

        return text;
    }
}
