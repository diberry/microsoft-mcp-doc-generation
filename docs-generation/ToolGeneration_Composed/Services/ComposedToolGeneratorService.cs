// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using ToolGeneration_Composed.Models;

namespace ToolGeneration_Composed.Services;

/// <summary>
/// Service that composes tool documentation by replacing placeholders with actual content
/// </summary>
public class ComposedToolGeneratorService
{
    private const string ExamplePromptsPlaceholder = "{{EXAMPLE_PROMPTS_CONTENT}}";
    private const string ParametersPlaceholder = "{{PARAMETERS_CONTENT}}";
    private const string AnnotationsPlaceholder = "{{ANNOTATIONS_CONTENT}}";

    /// <summary>
    /// Generates composed tool files by replacing placeholders in raw files with actual content
    /// </summary>
    public async Task<int> GenerateComposedToolFilesAsync(
        string rawToolsDir,
        string outputDir,
        string annotationsDir,
        string parametersDir,
        string examplePromptsDir)
    {
        Console.WriteLine("\n┌─────────────────────────────────────────────┐");
        Console.WriteLine("│  Generating Composed Tool Files            │");
        Console.WriteLine("└─────────────────────────────────────────────┘");

        // Validate input directories exist
        if (!Directory.Exists(rawToolsDir))
        {
            Console.Error.WriteLine($"Error: Raw tools directory not found: {rawToolsDir}");
            return 1;
        }

        Console.WriteLine($"  Raw Tools Directory: {rawToolsDir}");
        Console.WriteLine($"  Annotations Directory: {annotationsDir}");
        Console.WriteLine($"  Parameters Directory: {parametersDir}");
        Console.WriteLine($"  Example Prompts Directory: {examplePromptsDir}");
        Console.WriteLine($"  Output Directory: {outputDir}");
        Console.WriteLine();

        // Check for missing prerequisite directories and warn
        var missingDirs = new List<string>();
        if (!Directory.Exists(annotationsDir))
        {
            Console.WriteLine($"  ⚠ Annotations directory not found: {annotationsDir}");
            missingDirs.Add("annotations (run Step 1)");
        }
        else
        {
            var annotationFiles = Directory.GetFiles(annotationsDir, "*.md").Length;
            Console.WriteLine($"  ✓ Annotations directory found: {annotationFiles} files");
        }
        if (!Directory.Exists(parametersDir))
        {
            Console.WriteLine($"  ⚠ Parameters directory not found: {parametersDir}");
            missingDirs.Add("parameters (run Step 1)");
        }
        else
        {
            var parameterFiles = Directory.GetFiles(parametersDir, "*.md").Length;
            Console.WriteLine($"  ✓ Parameters directory found: {parameterFiles} files");
        }
        if (!Directory.Exists(examplePromptsDir))
        {
            Console.WriteLine($"  ⚠ Example prompts directory not found: {examplePromptsDir}");
            missingDirs.Add("example-prompts (run Step 2)");
        }
        else
        {
            var promptFiles = Directory.GetFiles(examplePromptsDir, "*.md").Length;
            Console.WriteLine($"  ✓ Example prompts directory found: {promptFiles} files");
        }
        if (missingDirs.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Note: Composition will complete but will use placeholder content for missing files.");
            Console.WriteLine();
        }

        // Ensure output directory exists
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            Console.WriteLine($"  Created output directory: {outputDir}");
        }

        // Get all raw tool files
        var rawFiles = Directory.GetFiles(rawToolsDir, "*.md");
        if (rawFiles.Length == 0)
        {
            Console.WriteLine("  No raw tool files found.");
            return 0;
        }

        Console.WriteLine($"  Found {rawFiles.Length} raw tool files to process");
        Console.WriteLine();

        int successCount = 0;
        int skippedCount = 0;
        var missingContent = new Dictionary<string, List<string>>();

        foreach (var rawFilePath in rawFiles)
        {
            var fileName = Path.GetFileName(rawFilePath);
            
            try
            {
                // Load raw file content
                var rawContent = await File.ReadAllTextAsync(rawFilePath);

                // Find and load corresponding content files
                var baseFileName = Path.GetFileNameWithoutExtension(fileName);
                
                var examplePromptsContent = await LoadContentFileAsync(
                    examplePromptsDir, baseFileName, "example-prompts", missingContent);
                    
                var parametersContent = await LoadContentFileAsync(
                    parametersDir, baseFileName, "parameters", missingContent);
                    
                var annotationsContent = await LoadContentFileAsync(
                    annotationsDir, baseFileName, "annotations", missingContent);

                // Compose the final content
                var composedContent = ComposeContent(
                    rawContent,
                    examplePromptsContent,
                    parametersContent,
                    annotationsContent);

                // Write composed file
                var outputPath = Path.Combine(outputDir, fileName);
                await File.WriteAllTextAsync(outputPath, composedContent);
                
                successCount++;
                
                if (successCount % 50 == 0)
                {
                    Console.WriteLine($"  Composed {successCount} files...");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"  Error processing {fileName}: {ex.Message}");
                skippedCount++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  ✓ Composed {successCount} tool files");
        
        if (skippedCount > 0)
        {
            Console.WriteLine($"  ⚠ Skipped {skippedCount} files due to errors");
        }

        // Report missing content files
        if (missingContent.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Missing Content Files:");
            foreach (var kvp in missingContent)
            {
                Console.WriteLine($"    {kvp.Key}: {kvp.Value.Count} missing");
            }
        }

        return 0;
    }

    private async Task<string> LoadContentFileAsync(
        string contentDir,
        string baseFileName,
        string contentType,
        Dictionary<string, List<string>> missingContent)
    {
        // Try to find the content file - it might have variations
        var possibleFiles = new[]
        {
            // Exact match with suffix
            Path.Combine(contentDir, $"{baseFileName}-{contentType}.md"),
            // Without suffix
            Path.Combine(contentDir, $"{baseFileName}.md"),
            // With azure- prefix (from Step 1 generation)
            Path.Combine(contentDir, $"azure-{baseFileName}-{contentType}.md"),
            // List all files in the directory and try fuzzy match as last resort
        };

        foreach (var filePath in possibleFiles)
        {
            if (File.Exists(filePath))
            {
                var content = await File.ReadAllTextAsync(filePath);
                return StripFrontmatter(content);
            }
        }

        // If exact matches fail, search for files containing the base name
        try
        {
            var allFiles = Directory.GetFiles(contentDir, "*.md");
            var matchingFiles = allFiles.Where(f => 
            {
                var fileName = Path.GetFileName(f);
                // Look for files that contain the base name and content type
                return fileName.Contains(baseFileName.Split('-').Last(), StringComparison.OrdinalIgnoreCase) && 
                       fileName.Contains(contentType, StringComparison.OrdinalIgnoreCase);
            }).ToArray();

            if (matchingFiles.Length > 0)
            {
                var content = await File.ReadAllTextAsync(matchingFiles[0]);
                return StripFrontmatter(content);
            }
        }
        catch
        {
            // Ignore errors during fallback search
        }

        // Content file not found - track it
        if (!missingContent.ContainsKey(contentType))
        {
            missingContent[contentType] = new List<string>();
        }
        missingContent[contentType].Add(baseFileName);

        return $"<!-- Content not found: {contentType} -->";
    }

    private string StripFrontmatter(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        // Check if content starts with frontmatter (---)
        if (!content.TrimStart().StartsWith("---"))
            return content;

        var lines = content.Split('\n');
        int startLine = 0;
        int endLine = -1;

        // Find start of frontmatter
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                startLine = i;
                break;
            }
        }

        // Find end of frontmatter
        for (int i = startLine + 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                endLine = i;
                break;
            }
        }

        if (endLine > startLine)
        {
            // Return content after frontmatter
            return string.Join('\n', lines.Skip(endLine + 1)).TrimStart();
        }

        return content;
    }

    private string ComposeContent(
        string rawContent,
        string examplePromptsContent,
        string parametersContent,
        string annotationsContent)
    {
        // Replace placeholders with actual content
        var composed = rawContent;
        
        composed = composed.Replace(ExamplePromptsPlaceholder, examplePromptsContent.Trim());
        composed = composed.Replace(ParametersPlaceholder, parametersContent.Trim());
        composed = composed.Replace(AnnotationsPlaceholder, annotationsContent.Trim());

        return composed;
    }
}
