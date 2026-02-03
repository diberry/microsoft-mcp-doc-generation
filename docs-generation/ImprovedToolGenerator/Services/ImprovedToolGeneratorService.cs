// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using ImprovedToolGenerator.Models;

namespace ImprovedToolGenerator.Services;

/// <summary>
/// Service that improves tool documentation using AI based on Microsoft content guidelines
/// </summary>
public class ImprovedToolGeneratorService
{
    private readonly GenerativeAIClient _aiClient;
    private readonly string _systemPrompt;
    private readonly string _userPromptTemplate;

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

                // Generate user prompt with the content
                var userPrompt = string.Format(_userPromptTemplate, originalContent);

                // Call AI to improve the content
                var improvedContent = await _aiClient.GetChatCompletionAsync(
                    _systemPrompt,
                    userPrompt,
                    maxTokens);

                // Save improved content
                var outputPath = Path.Combine(outputDir, fileName);
                await File.WriteAllTextAsync(outputPath, improvedContent);

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
}
