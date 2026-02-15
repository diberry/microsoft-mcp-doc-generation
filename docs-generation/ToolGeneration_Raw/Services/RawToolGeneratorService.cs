// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using ToolGeneration_Raw.Models;
using NaturalLanguageGenerator;
using Shared;

namespace ToolGeneration_Raw.Services;

/// <summary>
/// Service that generates raw tool documentation files with placeholders
/// </summary>
public class RawToolGeneratorService
{
    private readonly Dictionary<string, BrandMapping> _brandMappings;
    private readonly Dictionary<string, string> _compoundWords;
    private readonly HashSet<string> _stopWords;

    public RawToolGeneratorService(
        Dictionary<string, BrandMapping> brandMappings,
        Dictionary<string, string> compoundWords,
        HashSet<string> stopWords)
    {
        _brandMappings = brandMappings;
        _compoundWords = compoundWords;
        _stopWords = stopWords;
    }

    /// <summary>
    /// Generates raw tool files with placeholders from CLI output
    /// </summary>
    public async Task<int> GenerateRawToolFilesAsync(
        string cliOutputFile,
        string outputDir,
        string mcpCliVersion)
    {
        Console.WriteLine("\n┌─────────────────────────────────────────────┐");
        Console.WriteLine("│  Generating Raw Tool Files                 │");
        Console.WriteLine("└─────────────────────────────────────────────┘");

        // Load CLI output
        var cliData = await LoadCliOutputAsync(cliOutputFile);
        if (cliData == null)
        {
            Console.Error.WriteLine("Failed to load CLI output");
            return 1;
        }

        if (cliData.Results.Count == 0)
        {
            Console.Error.WriteLine("No tools found in CLI output. Check the CLI output JSON format.");
            return 1;
        }

        Console.WriteLine($"  Loaded {cliData.Results.Count} tools from CLI output");

        // Ensure output directory exists
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            Console.WriteLine($"  Created output directory: {outputDir}");
        }

        int generatedCount = 0;
        int skippedCount = 0;

        foreach (var tool in cliData.Results)
        {
            if (string.IsNullOrEmpty(tool.Command))
            {
                skippedCount++;
                continue;
            }

            try
            {
                var fileName = GenerateFileName(tool);
                var rawToolData = CreateRawToolData(tool, fileName, mcpCliVersion);
                var content = GenerateRawToolContent(rawToolData);

                var outputPath = Path.Combine(outputDir, fileName);
                await File.WriteAllTextAsync(outputPath, content);
                
                generatedCount++;
                
                if (generatedCount % 50 == 0)
                {
                    Console.WriteLine($"  Generated {generatedCount} files...");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Error generating file for {tool.Command}: {ex.Message}");
                skippedCount++;
            }
        }

        Console.WriteLine($"\n  ✓ Generated {generatedCount} raw tool files");
        if (skippedCount > 0)
        {
            Console.WriteLine($"  ⚠ Skipped {skippedCount} tools");
        }

        return 0;
    }

    private async Task<CliOutput?> LoadCliOutputAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return System.Text.Json.JsonSerializer.Deserialize<CliOutput>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading CLI output: {ex.Message}");
            return null;
        }
    }

    private string GenerateFileName(Tool tool)
    {
        return ToolFileNameBuilder.BuildToolFileName(
            tool.Command ?? "", _brandMappings, _compoundWords, _stopWords);
    }

    private RawToolData CreateRawToolData(Tool tool, string fileName, string mcpCliVersion)
    {
        var generatedDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        
        return new RawToolData
        {
            ToolName = tool.Name,
            Command = tool.Command,
            Description = tool.Description,
            FileName = fileName,
            GeneratedDate = generatedDate,
            McpCliVersion = mcpCliVersion,
            ExamplePromptsPlaceholder = "{{EXAMPLE_PROMPTS_CONTENT}}",
            ParametersPlaceholder = "{{PARAMETERS_CONTENT}}",
            AnnotationsPlaceholder = "{{ANNOTATIONS_CONTENT}}"
        };
    }

    private string GenerateRawToolContent(RawToolData data)
    {
        var sb = new StringBuilder();

        // Add frontmatter
        sb.AppendLine("---");
        sb.AppendLine("ms.topic: reference");
        sb.AppendLine($"ms.date: {data.GeneratedDate}");
        sb.AppendLine($"mcp-cli.version: {data.McpCliVersion}");
        sb.AppendLine($"generated: {data.GeneratedDate}");
        sb.AppendLine("---");
        sb.AppendLine();

        // Add title
        sb.AppendLine($"# {data.ToolName}");
        sb.AppendLine();

        // Add command comment
        sb.AppendLine($"<!-- @mcpcli {data.Command} -->");
        sb.AppendLine();

        // Add description
        if (!string.IsNullOrEmpty(data.Description))
        {
            sb.AppendLine(data.Description);
            sb.AppendLine();
        }

        // Add example prompts section with placeholder
        sb.AppendLine("Example prompts include:");
        sb.AppendLine();
        sb.AppendLine(data.ExamplePromptsPlaceholder);
        sb.AppendLine();

        // Add parameters section with placeholder
        sb.AppendLine(data.ParametersPlaceholder);
        sb.AppendLine();

        // Add annotations section with placeholder
        sb.AppendLine("[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):");
        sb.AppendLine();
        sb.AppendLine(data.AnnotationsPlaceholder);
        sb.AppendLine();

        return sb.ToString();
    }
}
