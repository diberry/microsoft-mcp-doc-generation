// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using ToolGeneration_Raw.Services;
using Shared;

namespace ToolGeneration_Raw;

internal class Program
{
    private const string ToolsRawDirectoryName = "tools-raw";
    
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("RawToolGenerator - Generate raw tool documentation with placeholders");
        Console.WriteLine("======================================================================");
        Console.WriteLine();

        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: RawToolGenerator <cli-output-json> <output-dir> [mcp-cli-version]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Arguments:");
            Console.Error.WriteLine("  cli-output-json    Path to CLI output JSON file");
            Console.Error.WriteLine("  output-dir         Output directory for raw tool files");
            Console.Error.WriteLine("  mcp-cli-version    Optional MCP CLI version (default: unknown)");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Example:");
            Console.Error.WriteLine($"  RawToolGenerator ./generated/cli/cli-output.json ./generated/{ToolsRawDirectoryName} 2.0.0-beta.13");
            return 1;
        }

        var cliOutputFile = args[0];
        var outputDir = args[1];
        
        // Get version from arguments or read from cli-version.json
        string mcpCliVersion;
        if (args.Length > 2)
        {
            mcpCliVersion = args[2];
        }
        else
        {
            // Extract base output directory (remove tools-raw suffix if present)
            var baseOutputDir = outputDir.EndsWith(ToolsRawDirectoryName) 
                ? Path.GetDirectoryName(outputDir) ?? outputDir
                : outputDir;
            mcpCliVersion = await CliVersionReader.ReadCliVersionAsync(baseOutputDir);
        }

        // Validate input file exists
        if (!File.Exists(cliOutputFile))
        {
            Console.Error.WriteLine($"Error: CLI output file not found: {cliOutputFile}");
            return 1;
        }

        Console.WriteLine($"CLI Output File: {cliOutputFile}");
        Console.WriteLine($"Output Directory: {outputDir}");
        Console.WriteLine($"MCP CLI Version: {mcpCliVersion}");
        Console.WriteLine();

        try
        {
            // Load brand mappings
            var brandMappings = await DataFileLoader.LoadBrandMappingsAsync();
            Console.WriteLine($"Loaded {brandMappings.Count} brand mappings");
            Console.WriteLine();

            // Create generator service
            var generator = new RawToolGeneratorService(brandMappings);

            // Generate raw tool files
            var result = await generator.GenerateRawToolFilesAsync(cliOutputFile, outputDir, mcpCliVersion);

            Console.WriteLine();
            Console.WriteLine(result == 0 ? "✓ Generation completed successfully" : "✗ Generation completed with errors");
            
            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    // LoadBrandMappingsAsync removed - now using Shared.DataFileLoader.LoadBrandMappingsAsync()
}
