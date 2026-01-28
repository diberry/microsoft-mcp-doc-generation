// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using ToolFamilyCleanup.Services;

namespace ToolFamilyCleanup;

/// <summary>
/// Standalone tool for cleaning up tool family documentation files using LLM-based processing.
/// Applies Microsoft style guide standards to generated documentation.
/// </summary>
internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Azure MCP Tool Family Cleanup");
        Console.WriteLine("============================");
        Console.WriteLine();

        try
        {
            // Parse command-line arguments
            var (config, useMultiPhase) = ParseArguments(args);

            // Load Azure OpenAI configuration
            var aiOptions = GenerativeAIOptions.LoadFromEnvironmentOrDotEnv();
            var missingVars = ValidateAIOptions(aiOptions);

            if (missingVars.Any())
            {
                Console.Error.WriteLine("Error: Missing required environment variables (after .env fallback):");
                foreach (var varName in missingVars)
                {
                    Console.Error.WriteLine($"  - {varName}");
                }
                Console.Error.WriteLine();
                Console.Error.WriteLine("Set these environment variables or create a .env file before running this tool.");
                return 1;
            }

            Console.WriteLine("✓ Environment variables loaded and validated");
            Console.WriteLine();

            if (useMultiPhase)
            {
                Console.WriteLine($"Mode:             Multi-phase (tool-level assembly)");
                Console.WriteLine($"Tools input:      {config.ToolsInputDirectory}");
                Console.WriteLine($"Metadata output:  {config.MetadataOutputDirectory}");
                Console.WriteLine($"Related output:   {config.RelatedContentOutputDirectory}");
                Console.WriteLine($"Final output:     {config.MultiFileOutputDirectory}");
            }
            else
            {
                Console.WriteLine($"Mode:             Single-phase (full file)");
                Console.WriteLine($"Input directory:  {config.InputDirectory}");
                Console.WriteLine($"Prompts output:   {config.PromptsOutputDirectory}");
                Console.WriteLine($"Cleanup output:   {config.CleanupOutputDirectory}");
            }
            Console.WriteLine();

            // Run cleanup generator
            var generator = new CleanupGenerator(aiOptions, config);
            
            if (useMultiPhase)
            {
                await generator.ProcessToolFamiliesMultiPhase();
            }
            else
            {
                await generator.ProcessAllToolFamilyFiles();
            }

            Console.WriteLine();
            Console.WriteLine("✓ Tool family cleanup completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static (CleanupConfiguration config, bool useMultiPhase) ParseArguments(string[] args)
    {
        var config = new CleanupConfiguration();
        bool useMultiPhase = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--multi-phase":
                case "-m":
                    useMultiPhase = true;
                    break;

                case "--input-dir":
                case "-i":
                    if (i + 1 < args.Length)
                    {
                        config.InputDirectory = args[++i];
                    }
                    break;

                case "--prompts-dir":
                case "-p":
                    if (i + 1 < args.Length)
                    {
                        config.PromptsOutputDirectory = args[++i];
                    }
                    break;

                case "--output-dir":
                case "-o":
                    if (i + 1 < args.Length)
                    {
                        config.CleanupOutputDirectory = args[++i];
                    }
                    break;

                case "--help":
                case "-h":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
            }
        }

        return (config, useMultiPhase);
    }

    private static List<string> ValidateAIOptions(GenerativeAIOptions options)
    {
        var missing = new List<string>();
        if (string.IsNullOrEmpty(options.ApiKey)) missing.Add("FOUNDRY_API_KEY");
        if (string.IsNullOrEmpty(options.Endpoint)) missing.Add("FOUNDRY_ENDPOINT");
        if (string.IsNullOrEmpty(options.Deployment)) missing.Add("TOOL_FAMILY_CLEANUP_TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME");
        return missing;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: ToolFamilyCleanup [options]");
        Console.WriteLine();
        Console.WriteLine("Modes:");
        Console.WriteLine("  Single-phase:  Process complete tool family files (default)");
        Console.WriteLine("  Multi-phase:   Assemble from individual tool files (--multi-phase)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -m, --multi-phase          Enable multi-phase mode (tool-level assembly)");
        Console.WriteLine("                             Reads from ../generated/tools and assembles families");
        Console.WriteLine();
        Console.WriteLine("  -i, --input-dir <path>     Input directory (single-phase mode)");
        Console.WriteLine("                             Default: ../generated/tool-family");
        Console.WriteLine();
        Console.WriteLine("  -p, --prompts-dir <path>   Directory to save generated prompts (single-phase)");
        Console.WriteLine("                             Default: ../generated/tool-family-cleanup-prompts");
        Console.WriteLine();
        Console.WriteLine("  -o, --output-dir <path>    Directory to save cleaned files (single-phase)");
        Console.WriteLine("                             Default: ../generated/tool-family-cleaned");
        Console.WriteLine();
        Console.WriteLine("  -h, --help                 Display this help message");
        Console.WriteLine();
        Console.WriteLine("Environment variables (or .env file):");
        Console.WriteLine("  FOUNDRY_API_KEY            Azure OpenAI API key");
        Console.WriteLine("  FOUNDRY_ENDPOINT           Azure OpenAI endpoint URL");
        Console.WriteLine("  TOOL_FAMILY_CLEANUP_TOOL_FAMILY_CLEANUP_FOUNDRY_MODEL_NAME         Azure OpenAI deployment/model name");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ToolFamilyCleanup                    # Single-phase with defaults");
        Console.WriteLine("  ToolFamilyCleanup --multi-phase      # Multi-phase mode (solves 16K token limit)");
    }
}
