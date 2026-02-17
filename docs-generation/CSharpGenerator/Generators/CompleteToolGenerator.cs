// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CSharpGenerator.Models;
using NaturalLanguageGenerator;
using Shared;
using TemplateEngine;

namespace CSharpGenerator.Generators;

/* DEPRECATED: CompleteToolGenerator has been replaced by ToolGeneration_Composed package
 * 
 * This generator was used to create complete tool documentation by:
 * - Reading annotation, parameter, and example-prompts files
 * - Stripping frontmatter
 * - Embedding content into a single complete file
 * 
 * Replacement: Use ToolGeneration_Raw -> ToolGeneration_Composed -> ToolGeneration_Improved pipeline
 * - ToolGeneration_Raw creates files with placeholders
 * - ToolGeneration_Composed replaces placeholders with actual content (same functionality as this)
 * - ToolGeneration_Improved applies AI enhancements
 * 
 * The new approach is more modular and separates concerns better.
 * 
/// <summary>
/// Generates complete tool documentation files that combine example prompts, annotations, and parameters.
/// Creates files in ./generated/tools/ directory with format: {tool}.complete.md
/// </summary>
public class CompleteToolGenerator
{
    private readonly Func<Task<Dictionary<string, BrandMapping>>> _loadBrandMappings;
    private readonly Func<string, Task<string>> _cleanFileName;

    public CompleteToolGenerator(
        Func<Task<Dictionary<string, BrandMapping>>> loadBrandMappings,
        Func<string, Task<string>> cleanFileName)
    {
        _loadBrandMappings = loadBrandMappings;
        _cleanFileName = cleanFileName;
    }

    /// <summary>
    /// Generates complete tool documentation files combining example prompts, annotations, and parameters.
    /// </summary>
    /// <param name="data">Transformed documentation data containing all tools</param>
    /// <param name="outputDir">Output directory for complete tool files (./generated/tools/)</param>
    /// <param name="templateFile">Path to the Handlebars template file</param>
    /// <param name="annotationsDir">Directory containing annotation files</param>
    /// <param name="parametersDir">Directory containing parameter files</param>
    /// <param name="examplePromptsDir">Directory containing example prompt files</param>
    public async Task GenerateCompleteToolFilesAsync(
        TransformedData data,
        string outputDir,
        string templateFile,
        string annotationsDir,
        string parametersDir,
        string examplePromptsDir)
    {
        Console.WriteLine("\n┌─────────────────────────────────────────────┐");
        Console.WriteLine("│  Generating Complete Tool Files            │");
        Console.WriteLine("└─────────────────────────────────────────────┘");

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            Console.WriteLine($"  Created output directory: {outputDir}");
        }

        var brandMappings = await _loadBrandMappings();
        Console.WriteLine($"  Loaded {brandMappings.Count} brand mappings");

        int generatedCount = 0;
        int skippedCount = 0;
        var missingFiles = new List<string>();

        foreach (var tool in data.Tools)
        {
            if (string.IsNullOrEmpty(tool.Command))
            {
                skippedCount++;
                continue;
            }

            try
            {
                // Parse command to build filename (same logic as annotation generator)
                var commandParts = tool.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (commandParts.Length == 0)
                {
                    skippedCount++;
                    continue;
                }

                var area = commandParts[0];

                // Get brand-based filename from mapping
                string brandFileName;
                if (brandMappings.TryGetValue(area, out var mapping) && !string.IsNullOrEmpty(mapping.FileName))
                {
                    brandFileName = mapping.FileName;
                }
                else
                {
                    brandFileName = area.ToLowerInvariant();
                }

                // Build remaining parts
                var remainingParts = commandParts.Length > 1 
                    ? string.Join("-", commandParts.Skip(1)).ToLowerInvariant()
                    : "";

                // Clean the filename to match the annotation/parameter file generation
                var cleanedRemainingParts = !string.IsNullOrEmpty(remainingParts) 
                    ? await _cleanFileName(remainingParts) 
                    : "";

                // Build filenames for all include files
                var baseFileName = !string.IsNullOrEmpty(cleanedRemainingParts)
                    ? $"{brandFileName}-{cleanedRemainingParts}"
                    : brandFileName;

                var annotationsFileName = $"{baseFileName}-annotations.md";
                var parametersFileName = $"{baseFileName}-parameters.md";
                var examplePromptsFileName = $"{baseFileName}-example-prompts.md";
                var completeFileName = $"{baseFileName}.complete.md";

                // Check if source files exist
                var annotationsPath = Path.Combine(annotationsDir, annotationsFileName);
                var parametersPath = Path.Combine(parametersDir, parametersFileName);
                var examplePromptsPath = Path.Combine(examplePromptsDir, examplePromptsFileName);

                var filesExist = true;
                if (!File.Exists(annotationsPath))
                {
                    missingFiles.Add($"Annotations: {annotationsFileName}");
                    filesExist = false;
                }
                if (!File.Exists(parametersPath))
                {
                    missingFiles.Add($"Parameters: {parametersFileName}");
                    filesExist = false;
                }
                // Example prompts are optional - only warn if missing
                var hasExamplePrompts = File.Exists(examplePromptsPath);

                // Skip if critical files don't exist
                if (!filesExist)
                {
                    skippedCount++;
                    continue;
                }

                // Read content from source files
                var parametersContent = await File.ReadAllTextAsync(parametersPath);
                var examplePromptsContent = hasExamplePrompts 
                    ? await File.ReadAllTextAsync(examplePromptsPath)
                    : "";

                // Strip frontmatter from content (lines between --- markers)
                parametersContent = StripFrontmatter(parametersContent);
                if (!string.IsNullOrEmpty(examplePromptsContent))
                {
                    examplePromptsContent = StripFrontmatter(examplePromptsContent);
                }

                // Prepare template data
                var templateData = new Dictionary<string, object>
                {
                    ["command"] = tool.Command,
                    ["description"] = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(tool.Description ?? "")),
                    ["conditionalRequiredNote"] = tool.ConditionalRequiredNote ?? "",
                    ["annotationsFileName"] = annotationsFileName,
                    ["parametersFileName"] = parametersFileName,
                    ["examplePromptsFileName"] = examplePromptsFileName,
                    ["completeFileName"] = completeFileName,
                    ["hasExamplePrompts"] = hasExamplePrompts,
                    ["parametersContent"] = parametersContent,
                    ["examplePromptsContent"] = examplePromptsContent,
                    ["generatedAt"] = DateTime.UtcNow,
                    ["version"] = data.Version ?? "unknown"
                };

                // Generate the complete tool file
                var outputFile = Path.Combine(outputDir, completeFileName);
                var content = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, templateData);
                await File.WriteAllTextAsync(outputFile, content);

                generatedCount++;

                // Progress indicator every 20 files
                if (generatedCount % 20 == 0)
                {
                    Console.Write($"  Progress: {generatedCount} files generated...\r");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Error generating complete file for '{tool.Command}': {ex.Message}");
                skippedCount++;
            }
        }

        Console.WriteLine($"\n  ✅ Complete Tool Files Generated: {generatedCount}");
        
        if (skippedCount > 0)
        {
            Console.WriteLine($"  ⚠ Skipped: {skippedCount} (missing source files or invalid commands)");
        }

        if (missingFiles.Any())
        {
            Console.WriteLine($"  ℹ Missing source files (first 10):");
            foreach (var missing in missingFiles.Take(10))
            {
                Console.WriteLine($"    - {missing}");
            }
            if (missingFiles.Count > 10)
            {
                Console.WriteLine($"    ... and {missingFiles.Count - 10} more");
            }
        }

        Console.WriteLine($"  Output directory: {Path.GetFullPath(outputDir)}");
        Console.WriteLine();
    }

    /// <summary>
    /// Strips YAML frontmatter (content between --- markers) from markdown content
    /// </summary>
    private static string StripFrontmatter(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        var lines = content.Split('\n');
        var frontmatterCount = 0;
        var contentStart = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                frontmatterCount++;
                if (frontmatterCount == 2)
                {
                    // End of frontmatter found
                    contentStart = i + 1;
                    break;
                }
            }
        }

        // Return content after frontmatter, trimming leading whitespace
        return contentStart > 0 
            ? string.Join("\n", lines.Skip(contentStart)).TrimStart()
            : content;
    }
}
*/