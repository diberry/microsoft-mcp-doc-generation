// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;
using Shared;
using ToolGeneration_Composed.Models;

namespace ToolGeneration_Composed.Services;

/// <summary>
/// Service that composes tool documentation by replacing placeholders with actual content.
/// Uses ToolFileNameBuilder for deterministic filename resolution (no fuzzy matching).
/// </summary>
public class ComposedToolGeneratorService
{
    private const string ExamplePromptsPlaceholder = "{{EXAMPLE_PROMPTS_CONTENT}}";
    private const string ParametersPlaceholder = "{{PARAMETERS_CONTENT}}";
    private const string AnnotationsPlaceholder = "{{ANNOTATIONS_CONTENT}}";

    private static readonly Regex McpCliCommentRegex = new(
        @"<!--\s*@mcpcli\s+(.+?)\s*-->", RegexOptions.Compiled);

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

        // Load shared data files for deterministic filename resolution
        var nameContext = await FileNameContext.CreateAsync();

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

                // Extract command from <!-- @mcpcli ... --> comment for deterministic filename resolution
                var command = ExtractCommand(rawContent);

                string examplePromptsContent, parametersContent, annotationsContent;

                if (!string.IsNullOrEmpty(command))
                {
                    // Use ToolFileNameBuilder for exact filename derivation
                    var examplePromptsFileName = ToolFileNameBuilder.BuildExamplePromptsFileName(
                        command, nameContext);
                    var parametersFileName = ToolFileNameBuilder.BuildParameterFileName(
                        command, nameContext);
                    var annotationsFileName = ToolFileNameBuilder.BuildAnnotationFileName(
                        command, nameContext);

                    examplePromptsContent = await LoadExactFileAsync(
                        examplePromptsDir, examplePromptsFileName, "example-prompts", fileName, missingContent);
                    parametersContent = await LoadExactFileAsync(
                        parametersDir, parametersFileName, "parameters", fileName, missingContent);
                    annotationsContent = await LoadExactFileAsync(
                        annotationsDir, annotationsFileName, "annotations", fileName, missingContent);
                }
                else
                {
                    // No command found in raw file - warn on console and track
                    Console.WriteLine($"  ⚠ No @mcpcli comment found in {fileName}");
                    LogFileHelper.WriteDebug($"No @mcpcli comment found in {fileName}");
                    examplePromptsContent = "<!-- Content not found: example-prompts (no @mcpcli command) -->";
                    parametersContent = "<!-- Content not found: parameters (no @mcpcli command) -->";
                    annotationsContent = "<!-- Content not found: annotations (no @mcpcli command) -->";
                }

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

    /// <summary>
    /// Extracts the CLI command from the raw file's &lt;!-- @mcpcli ... --&gt; comment.
    /// </summary>
    private static string? ExtractCommand(string rawContent)
    {
        var match = McpCliCommentRegex.Match(rawContent);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Loads a content file by exact filename (no fuzzy matching).
    /// </summary>
    private static async Task<string> LoadExactFileAsync(
        string contentDir,
        string exactFileName,
        string contentType,
        string rawFileName,
        Dictionary<string, List<string>> missingContent)
    {
        if (!Directory.Exists(contentDir))
        {
            return $"<!-- Content not found: {contentType} (directory missing) -->";
        }

        var filePath = Path.Combine(contentDir, exactFileName);
        if (File.Exists(filePath))
        {
            var content = await File.ReadAllTextAsync(filePath);
            return StripFrontmatter(content);
        }

        // Content file not found - track it
        if (!missingContent.ContainsKey(contentType))
        {
            missingContent[contentType] = new List<string>();
        }
        missingContent[contentType].Add($"{rawFileName} -> {exactFileName}");

        return $"<!-- Content not found: {contentType} -->";
    }

    private static string StripFrontmatter(string content)
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

    private static string ComposeContent(
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
