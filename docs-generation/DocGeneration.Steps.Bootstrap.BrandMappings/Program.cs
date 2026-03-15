// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using GenerativeAI;
using Shared;

namespace BrandMapperValidator;

/// <summary>
/// Validates that all MCP CLI namespaces have brand mappings.
/// If new namespaces are found, uses GenAI to suggest brand mapping entries.
/// Outputs suggested JSON for human review and halts execution.
/// </summary>
internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Azure MCP Brand Mapping Validator");
        Console.WriteLine("==================================");
        Console.WriteLine();

        try
        {
            // Parse arguments
            var (cliOutputPath, brandMappingPath, outputPath) = ParseArguments(args);
            
            LogFileHelper.Initialize("brand-mapper-validator");

            Console.WriteLine($"CLI output:     {cliOutputPath}");
            Console.WriteLine($"Brand mapping:  {brandMappingPath}");
            Console.WriteLine($"Output:         {outputPath}");
            Console.WriteLine();

            // Step 1: Load CLI output and extract unique namespaces
            if (!File.Exists(cliOutputPath))
            {
                Console.Error.WriteLine($"Error: CLI output file not found: {cliOutputPath}");
                return 1;
            }

            var cliJson = await File.ReadAllTextAsync(cliOutputPath);
            var cliOutput = JsonSerializer.Deserialize<CliOutput>(cliJson);
            if (cliOutput?.Results == null || cliOutput.Results.Count == 0)
            {
                Console.Error.WriteLine("Error: CLI output contains no tools.");
                return 1;
            }

            // Extract unique namespaces (first word of each command)
            var namespaces = cliOutput.Results
                .Where(r => !string.IsNullOrEmpty(r.Command))
                .Select(r => r.Command!.Split(' ')[0].ToLowerInvariant())
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            Console.WriteLine($"Found {namespaces.Count} unique namespaces in CLI output");

            // Step 2: Load existing brand mappings using shared loader
            var existingMappingsDict = await DataFileLoader.LoadBrandMappingsAsync();
            var existingMappings = existingMappingsDict.Values.ToList();

            var mappedNamespaces = new HashSet<string>(
                existingMappings.Select(m => m.McpServerName.ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase);

            Console.WriteLine($"Found {existingMappings.Count} existing brand mappings");
            Console.WriteLine();

            // Step 3: Find unmapped namespaces
            var unmappedNamespaces = namespaces
                .Where(ns => !mappedNamespaces.Contains(ns))
                .ToList();

            if (unmappedNamespaces.Count == 0)
            {
                Console.WriteLine("✅ All namespaces have brand mappings. No action needed.");
                Console.WriteLine();

                // Write empty result file to signal success
                await WriteResultFile(outputPath, new List<BrandMapping>(), existingMappings.Count, namespaces.Count);
                return 0;
            }

            Console.WriteLine($"⚠️  Found {unmappedNamespaces.Count} unmapped namespace(s):");
            foreach (var ns in unmappedNamespaces)
            {
                Console.WriteLine($"  - {ns}");
            }
            Console.WriteLine();

            // Step 4: Use GenAI to suggest brand mappings
            Console.WriteLine("Generating brand mapping suggestions using AI...");
            Console.WriteLine();

            var aiOptions = GenerativeAIOptions.LoadFromEnvironmentOrDotEnv();
            var missingVars = new List<string>();
            if (string.IsNullOrEmpty(aiOptions.ApiKey)) missingVars.Add("FOUNDRY_API_KEY");
            if (string.IsNullOrEmpty(aiOptions.Endpoint)) missingVars.Add("FOUNDRY_ENDPOINT");
            if (string.IsNullOrEmpty(aiOptions.Deployment)) missingVars.Add("FOUNDRY_MODEL_NAME");

            if (missingVars.Any())
            {
                Console.WriteLine("Warning: Azure OpenAI not configured. Generating placeholder mappings.");
                Console.WriteLine("Set environment variables or .env for AI-generated suggestions.");
                Console.WriteLine();

                var placeholders = unmappedNamespaces.Select(ns => new BrandMapping
                {
                    McpServerName = ns,
                    BrandName = $"Azure {FormatDisplayName(ns)}",
                    ShortName = FormatDisplayName(ns),
                    FileName = $"azure-{ns.Replace("_", "-")}"
                }).ToList();

                await WriteResultFile(outputPath, placeholders, existingMappings.Count, namespaces.Count);
                PrintSuggestions(placeholders, brandMappingPath);
                return 2; // Exit code 2 = new mappings need review
            }

            var aiClient = new GenerativeAIClient(aiOptions);

            // Load prompts
            var promptsDir = Path.Combine(AppContext.BaseDirectory, "prompts");
            var systemPrompt = await File.ReadAllTextAsync(Path.Combine(promptsDir, "system-prompt.txt"));
            var userPromptTemplate = await File.ReadAllTextAsync(Path.Combine(promptsDir, "user-prompt.txt"));

            // Generate example entries for context
            var exampleEntries = existingMappings
                .Take(5)
                .Select(m => JsonSerializer.Serialize(m, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            var examplesText = string.Join(",\n", exampleEntries);

            var suggestions = new List<BrandMapping>();

            foreach (var ns in unmappedNamespaces)
            {
                Console.Write($"  Generating mapping for '{ns}'... ");

                // Get tool commands for this namespace
                var toolCommands = cliOutput.Results
                    .Where(r => r.Command != null && r.Command.Split(' ')[0].Equals(ns, StringComparison.OrdinalIgnoreCase))
                    .Select(r => $"- {r.Command}: {r.Description?.Split('\n')[0]}")
                    .Take(10);
                var toolCommandsText = string.Join("\n", toolCommands);

                var userPrompt = userPromptTemplate
                    .Replace("{{NAMESPACE}}", ns)
                    .Replace("{{TOOL_COMMANDS}}", toolCommandsText)
                    .Replace("{{EXISTING_EXAMPLES}}", examplesText);

                try
                {
                    var response = await aiClient.GetChatCompletionAsync(systemPrompt, userPrompt, maxTokens: 500);

                    // Parse JSON from response
                    var mapping = ParseMappingFromResponse(response, ns);
                    if (mapping != null)
                    {
                        suggestions.Add(mapping);
                        Console.WriteLine($"✓ {mapping.BrandName}");
                    }
                    else
                    {
                        // Fallback
                        var fallback = new BrandMapping
                        {
                            McpServerName = ns,
                            BrandName = $"Azure {FormatDisplayName(ns)}",
                            ShortName = FormatDisplayName(ns),
                            FileName = $"azure-{ns.Replace("_", "-")}"
                        };
                        suggestions.Add(fallback);
                        Console.WriteLine($"⚠ Used fallback: {fallback.BrandName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error: {ex.Message}");
                    var fallback = new BrandMapping
                    {
                        McpServerName = ns,
                        BrandName = $"Azure {FormatDisplayName(ns)}",
                        ShortName = FormatDisplayName(ns),
                        FileName = $"azure-{ns.Replace("_", "-")}"
                    };
                    suggestions.Add(fallback);
                }
            }

            Console.WriteLine();

            // Step 5: Write results and halt
            await WriteResultFile(outputPath, suggestions, existingMappings.Count, namespaces.Count);
            PrintSuggestions(suggestions, brandMappingPath);

            return 2; // Exit code 2 = new mappings need human review
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static (string cliOutputPath, string brandMappingPath, string outputPath) ParseArguments(string[] args)
    {
        // Defaults
        var cliOutput = "../generated/cli/cli-output.json";
        var brandMapping = "data/brand-to-server-mapping.json";
        var output = "../generated/reports/brand-mapping-suggestions.json";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--cli-output" when i + 1 < args.Length:
                    cliOutput = args[++i];
                    break;
                case "--brand-mapping" when i + 1 < args.Length:
                    brandMapping = args[++i];
                    break;
                case "--output" when i + 1 < args.Length:
                    output = args[++i];
                    break;
            }
        }

        return (cliOutput, brandMapping, output);
    }

    private static BrandMapping? ParseMappingFromResponse(string response, string ns)
    {
        try
        {
            // Try to extract JSON from response
            var json = response.Trim();

            // Strip markdown code fences if present
            if (json.Contains("```"))
            {
                var startIdx = json.IndexOf('{');
                var endIdx = json.LastIndexOf('}');
                if (startIdx >= 0 && endIdx > startIdx)
                {
                    json = json.Substring(startIdx, endIdx - startIdx + 1);
                }
            }

            // Find JSON object boundaries
            var firstBrace = json.IndexOf('{');
            var lastBrace = json.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                json = json.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            var mapping = JsonSerializer.Deserialize<BrandMapping>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (mapping != null)
            {
                // Ensure mcpServerName is correct
                mapping.McpServerName = ns;
            }

            return mapping;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatDisplayName(string ns)
    {
        // Convert "azuremigrate" to "Azure Migrate", "advisor" to "Advisor"
        // Simple heuristic: split on common boundaries
        var words = new List<string>();
        var current = "";

        for (int i = 0; i < ns.Length; i++)
        {
            if (i > 0 && char.IsUpper(ns[i]))
            {
                if (current.Length > 0) words.Add(current);
                current = ns[i].ToString();
            }
            else
            {
                current += ns[i];
            }
        }
        if (current.Length > 0) words.Add(current);

        return string.Join(" ", words.Select(w =>
            char.ToUpper(w[0]) + w.Substring(1)));
    }

    private static void PrintSuggestions(List<BrandMapping> suggestions, string brandMappingPath)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("🛑 NEW BRAND MAPPINGS REQUIRE HUMAN REVIEW");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine($"Add the following entries to: {Path.GetFullPath(brandMappingPath)}");
        Console.WriteLine();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        for (int i = 0; i < suggestions.Count; i++)
        {
            var json = JsonSerializer.Serialize(suggestions[i], options);
            Console.WriteLine(json + ",");
            Console.WriteLine();
        }

        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("INSTRUCTIONS:");
        Console.WriteLine($"  1. Review the suggested mappings above");
        Console.WriteLine($"  2. Add approved entries to: {Path.GetFullPath(brandMappingPath)}");
        Console.WriteLine($"  3. Re-run the generation pipeline");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("⛔ PIPELINE HALTED - Cannot continue without complete brand mappings.");
        Console.WriteLine();
    }

    private static async Task WriteResultFile(string outputPath, List<BrandMapping> suggestions, int existingCount, int totalNamespaces)
    {
        var result = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            existingMappingCount = existingCount,
            totalNamespaces = totalNamespaces,
            newMappingsNeeded = suggestions.Count,
            suggestions = suggestions
        };

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await File.WriteAllTextAsync(outputPath, json);
        Console.WriteLine($"Suggestions saved to: {Path.GetFullPath(outputPath)}");
    }
}

// Models for CLI output deserialization
internal class CliOutput
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("results")]
    public List<CliTool> Results { get; set; } = new();
}

internal class CliTool
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
