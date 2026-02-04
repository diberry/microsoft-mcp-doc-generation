// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolGeneration_Composed.Services;

namespace ToolGeneration_Composed;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.WriteLine("ComposedToolGenerator - Compose tool documentation from components");
        Console.WriteLine("===================================================================");
        Console.WriteLine();

        if (args.Length < 5)
        {
            Console.Error.WriteLine("Usage: ComposedToolGenerator <raw-tools-dir> <output-dir> <annotations-dir> <parameters-dir> <example-prompts-dir>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Arguments:");
            Console.Error.WriteLine("  raw-tools-dir        Directory containing raw tool files with placeholders");
            Console.Error.WriteLine("  output-dir           Output directory for composed tool files");
            Console.Error.WriteLine("  annotations-dir      Directory containing annotation files");
            Console.Error.WriteLine("  parameters-dir       Directory containing parameter files");
            Console.Error.WriteLine("  example-prompts-dir  Directory containing example prompt files");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Example:");
            Console.Error.WriteLine("  ComposedToolGenerator \\");
            Console.Error.WriteLine("    ./generated/tools-raw \\");
            Console.Error.WriteLine("    ./generated/tools-composed \\");
            Console.Error.WriteLine("    ./generated/multi-page/annotations \\");
            Console.Error.WriteLine("    ./generated/multi-page/parameters \\");
            Console.Error.WriteLine("    ./generated/multi-page/example-prompts");
            return 1;
        }

        var rawToolsDir = args[0];
        var outputDir = args[1];
        var annotationsDir = args[2];
        var parametersDir = args[3];
        var examplePromptsDir = args[4];

        // Validate input directories
        if (!Directory.Exists(rawToolsDir))
        {
            Console.Error.WriteLine($"Error: Raw tools directory not found: {rawToolsDir}");
            return 1;
        }

        Console.WriteLine($"Raw Tools Directory: {rawToolsDir}");
        Console.WriteLine($"Output Directory: {outputDir}");
        Console.WriteLine($"Annotations Directory: {annotationsDir}");
        Console.WriteLine($"Parameters Directory: {parametersDir}");
        Console.WriteLine($"Example Prompts Directory: {examplePromptsDir}");
        Console.WriteLine();

        try
        {
            // Create generator service
            var generator = new ComposedToolGeneratorService();

            // Generate composed tool files
            var result = await generator.GenerateComposedToolFilesAsync(
                rawToolsDir,
                outputDir,
                annotationsDir,
                parametersDir,
                examplePromptsDir);

            Console.WriteLine();
            Console.WriteLine(result == 0 ? "✓ Composition completed successfully" : "✗ Composition completed with errors");
            
            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
