// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CSharpGenerator;
using HorizontalArticleGenerator.Generators;
using Shared;

namespace HorizontalArticleGenerator;

/// <summary>
/// Standalone entry point for horizontal article generation.
/// Does not modify existing Program.cs or documentation generation.
/// </summary>
internal class HorizontalArticleProgram
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Azure MCP Horizontal Article Generator");
        Console.WriteLine("======================================");
        Console.WriteLine();
        
        try
        {
            // Load config using existing infrastructure
            var configPath = Path.Combine(AppContext.BaseDirectory, "../../../../config.json");
            Console.WriteLine($"Loading config from: {configPath}");
            var success = Config.Load(configPath);
            if (!success)
            {
                Console.Error.WriteLine("Failed to load configuration.");
                return 1;
            }
            Console.WriteLine("✓ Configuration loaded");
            Console.WriteLine();
            
            // Load environment variables using GenerativeAIOptions (with .env fallback and diagnostics)
            var aiOptions = GenerativeAI.GenerativeAIOptions.LoadFromEnvironmentOrDotEnv();
            var missingVars = new List<string>();
            if (string.IsNullOrEmpty(aiOptions.ApiKey)) missingVars.Add("FOUNDRY_API_KEY");
            if (string.IsNullOrEmpty(aiOptions.Endpoint)) missingVars.Add("FOUNDRY_ENDPOINT");
            if (string.IsNullOrEmpty(aiOptions.Deployment)) missingVars.Add("FOUNDRY_MODEL_NAME");
            if (string.IsNullOrEmpty(aiOptions.ApiVersion)) missingVars.Add("FOUNDRY_MODEL_API_VERSION");

            if (missingVars.Any())
            {
                Console.Error.WriteLine("Error: Missing required environment variables (after .env fallback):");
                foreach (var varName in missingVars)
                {
                    Console.Error.WriteLine($"  - {varName}");
                }
                Console.Error.WriteLine();
                Console.Error.WriteLine("Set these environment variables before running this tool.");
                return 1;
            }
            Console.WriteLine("✓ Environment variables loaded and validated");
            Console.WriteLine();
            
            // Parse command-line arguments
            bool generateAllArticles = !args.Contains("--single"); // Default to all, use --single for testing
            bool useTextTransformation = args.Contains("--transform");
            
            if (generateAllArticles)
            {
                Console.WriteLine("Mode: Generating ALL horizontal articles");
            }
            else
            {
                Console.WriteLine("Mode: Generating SINGLE article (test mode, use without --single for all)");
            }
            Console.WriteLine();
            
            // Run generator
            var generator = new Generators.HorizontalArticleGenerator(aiOptions, useTextTransformation, generateAllArticles);
            await generator.GenerateAllArticles();
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
