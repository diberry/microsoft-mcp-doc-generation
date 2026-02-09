// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using System.Linq;
using ToolGeneration_Improved.Models;

namespace ToolGeneration_Improved.Services;

/// <summary>
/// Service that improves tool documentation using AI based on Microsoft content guidelines
/// </summary>
public class ImprovedToolGeneratorService
{
    private readonly GenerativeAIClient _aiClient;
    private readonly string _systemPrompt;
    private readonly string _userPromptTemplate;
    private static readonly string[] TemplateLabels =
    [
        "Example prompts include:",
        "Example prompts:",
        "Required options:",
        "Optional options:",
        "Required parameters:",
        "Optional parameters:",
        "**Prerequisites**:",
        "**Success verification**:",
        "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):",
        "[Tool annotation hints](../index.md#tool-annotations-for-azure-mcp-server):",
        "[Tool annotation hints](../../index.md#tool-annotations-for-azure-mcp-server):"
    ];

    public ImprovedToolGeneratorService(GenerativeAIClient aiClient, string systemPrompt, string userPromptTemplate)
    {
        _aiClient = aiClient;
        _systemPrompt = systemPrompt;
        _userPromptTemplate = userPromptTemplate;
    }

    /// <summary>
    /// Generates improved tool files using AI to apply Microsoft content guidelines
    /// </summary>
    public async Task<int> GenerateImprovedToolFilesAsync(
        string composedToolsDir,
        string outputDir,
        int maxTokens = 8000)
    {
        Console.WriteLine("\n┌─────────────────────────────────────────────┐");
        Console.WriteLine("│  Generating AI-Improved Tool Files         │");
        Console.WriteLine("└─────────────────────────────────────────────┘");

        // Validate input directory exists
        if (!Directory.Exists(composedToolsDir))
        {
            Console.Error.WriteLine($"Error: Composed tools directory not found: {composedToolsDir}");
            return 1;
        }

        Console.WriteLine($"  Composed Tools Directory: {composedToolsDir}");
        Console.WriteLine($"  Output Directory: {outputDir}");
        Console.WriteLine($"  Max Tokens: {maxTokens}");
        Console.WriteLine();

        // Ensure output directory exists
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            Console.WriteLine($"  Created output directory: {outputDir}");
        }

        // Get all composed tool files
        var composedFiles = Directory.GetFiles(composedToolsDir, "*.md");
        if (composedFiles.Length == 0)
        {
            Console.WriteLine("  No composed tool files found.");
            return 0;
        }

        Console.WriteLine($"  Found {composedFiles.Length} composed tool files to improve");
        Console.WriteLine();

        int successCount = 0;
        int skippedCount = 0;
        int errorCount = 0;

        for (int i = 0; i < composedFiles.Length; i++)
        {
            var composedFilePath = composedFiles[i];
            var fileName = Path.GetFileName(composedFilePath);
            var progress = $"[{i + 1}/{composedFiles.Length}]";

            try
            {
                Console.Write($"  {progress} Processing {fileName}...");

                // Load composed file content
                var originalContent = await File.ReadAllTextAsync(composedFilePath);

                // Protect handlebar template labels from AI modification
                var protectedContent = ProtectTemplateLabels(originalContent, out var labelMap);

                // Generate user prompt with the content
                var userPrompt = string.Format(_userPromptTemplate, protectedContent);

                // Call AI to improve the content
                var improvedContent = await _aiClient.GetChatCompletionAsync(
                    _systemPrompt,
                    userPrompt,
                    maxTokens);

                // Restore protected labels and normalize formatting
                var restoredContent = RestoreTemplateLabels(improvedContent, labelMap);
                restoredContent = NormalizeTemplateLabels(restoredContent);

                // Save improved content
                var outputPath = Path.Combine(outputDir, fileName);
                await File.WriteAllTextAsync(outputPath, restoredContent);

                successCount++;
                Console.WriteLine(" ✓");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("truncated"))
            {
                // Handle truncation error - save original instead
                Console.WriteLine($" ⚠ Truncated - saving original");
                Console.WriteLine($"      {ex.Message}");
                
                try
                {
                    var originalContent = await File.ReadAllTextAsync(composedFilePath);
                    var outputPath = Path.Combine(outputDir, fileName);
                    await File.WriteAllTextAsync(outputPath, originalContent);
                    skippedCount++;
                }
                catch (Exception saveEx)
                {
                    Console.WriteLine($"      Error saving original: {saveEx.Message}");
                    errorCount++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" ✗");
                Console.WriteLine($"      Error: {ex.Message}");
                errorCount++;
            }

            // Add a small delay between requests to avoid rate limiting
            if (i < composedFiles.Length - 1)
            {
                await Task.Delay(100);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  ✓ Successfully improved {successCount} tool files");
        
        if (skippedCount > 0)
        {
            Console.WriteLine($"  ⚠ Skipped {skippedCount} files (truncation - original saved)");
        }
        
        if (errorCount > 0)
        {
            Console.WriteLine($"  ✗ Failed to process {errorCount} files");
        }

        return errorCount > 0 ? 1 : 0;
    }

    private static string ProtectTemplateLabels(string content, out Dictionary<string, string> labelMap)
    {
        var map = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(content))
        {
            labelMap = map;
            return content;
        }

        var labelPattern = string.Join("|", TemplateLabels.Select(label => System.Text.RegularExpressions.Regex.Escape(label)));
        var regex = new System.Text.RegularExpressions.Regex(
            $@"^(\s*)({labelPattern})\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        var index = 0;
        var protectedContent = regex.Replace(content, match =>
        {
            var token = $"__TPL_LABEL_{index++}__";
            map[token] = match.Value;
            return token;
        });

        labelMap = map;
        return protectedContent;
    }

    private static string RestoreTemplateLabels(string content, Dictionary<string, string> labelMap)
    {
        if (string.IsNullOrEmpty(content) || labelMap.Count == 0)
        {
            return content;
        }

        var restored = content;
        foreach (var pair in labelMap)
        {
            restored = restored.Replace(pair.Key, pair.Value);
        }

        return restored;
    }

    private static string NormalizeTemplateLabels(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        var normalized = content;
        foreach (var label in TemplateLabels)
        {
            var labelText = label.Trim();
            var labelLiteral = System.Text.RegularExpressions.Regex.Escape(labelText.Trim('*'));
            var regex = new System.Text.RegularExpressions.Regex(
                $@"^(\s*)(\*\*|###\s+)?{labelLiteral}(\*\*)?\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            normalized = regex.Replace(normalized, $"$1{labelText}");
        }

        return normalized;
    }
}
