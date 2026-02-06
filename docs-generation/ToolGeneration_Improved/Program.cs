// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using ToolGeneration_Improved.Services;

namespace ToolGeneration_Improved;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("ImprovedToolGenerator - Apply AI-based improvements to tool documentation");
        Console.WriteLine("==========================================================================");
        Console.WriteLine();

        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: ImprovedToolGenerator <composed-tools-dir> <output-dir> [max-tokens]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Arguments:");
            Console.Error.WriteLine("  composed-tools-dir   Directory containing composed tool files");
            Console.Error.WriteLine("  output-dir           Output directory for AI-improved tool files");
            Console.Error.WriteLine("  max-tokens           Optional max tokens for AI response (default: 8000)");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Environment Variables Required:");
            Console.Error.WriteLine("  FOUNDRY_API_KEY      Azure OpenAI API key");
            Console.Error.WriteLine("  FOUNDRY_ENDPOINT     Azure OpenAI endpoint");
            Console.Error.WriteLine("  FOUNDRY_MODEL_NAME   Azure OpenAI deployment/model name");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Example:");
            Console.Error.WriteLine("  ImprovedToolGenerator \\");
            Console.Error.WriteLine("    ./generated/tools-composed \\");
            Console.Error.WriteLine("    ./generated/tools-ai-improved \\");
            Console.Error.WriteLine("    8000");
            return 1;
        }

        var composedToolsDir = args[0];
        var outputDir = args[1];
        var maxTokens = args.Length > 2 && int.TryParse(args[2], out var mt) ? mt : 8000;

        // Validate input directory
        if (!Directory.Exists(composedToolsDir))
        {
            Console.Error.WriteLine($"Error: Composed tools directory not found: {composedToolsDir}");
            return 1;
        }

        Console.WriteLine($"Composed Tools Directory: {composedToolsDir}");
        Console.WriteLine($"Output Directory: {outputDir}");
        Console.WriteLine($"Max Tokens: {maxTokens}");
        Console.WriteLine();

        try
        {
            // Load prompts
            var systemPrompt = await LoadPromptAsync("system-prompt.txt");
            var userPromptTemplate = await LoadPromptAsync("user-prompt-template.txt");

            Console.WriteLine("Loaded system and user prompt templates");
            Console.WriteLine();

            // Initialize AI client
            Console.WriteLine("Initializing Azure OpenAI client...");
            var aiClient = new GenerativeAIClient();
            Console.WriteLine("✓ Azure OpenAI client initialized");
            Console.WriteLine();

            // Create generator service
            var generator = new ImprovedToolGeneratorService(aiClient, systemPrompt, userPromptTemplate);

            // Generate improved tool files
            var result = await generator.GenerateImprovedToolFilesAsync(
                composedToolsDir,
                outputDir,
                maxTokens);

            Console.WriteLine();
            Console.WriteLine(result == 0 
                ? "✓ AI improvement completed successfully" 
                : "✗ AI improvement completed with errors");
            
            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static async Task<string> LoadPromptAsync(string promptFileName)
    {
        // Try to find the prompt file in multiple locations
        var searchPaths = new[]
        {
            // Relative to AppContext.BaseDirectory (bin/Release/net9.0)
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Prompts", promptFileName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ToolGeneration_Improved", "Prompts", promptFileName),
            
            // Relative to current working directory
            Path.Combine(Directory.GetCurrentDirectory(), "Prompts", promptFileName),
            Path.Combine(Directory.GetCurrentDirectory(), "ToolGeneration_Improved", "Prompts", promptFileName),
            Path.Combine(Directory.GetCurrentDirectory(), "docs-generation", "ToolGeneration_Improved", "Prompts", promptFileName),
            
            // Relative to project directory
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Prompts", promptFileName),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "ToolGeneration_Improved", "Prompts", promptFileName),
        };

        foreach (var path in searchPaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return await File.ReadAllTextAsync(fullPath);
            }
        }

        throw new FileNotFoundException($"Prompt file not found: {promptFileName}. Searched in multiple locations.");
    }
}
