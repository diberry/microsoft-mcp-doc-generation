// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Generates cleaned-up tool family documentation using LLM-based processing.
/// Reads tool family markdown files, generates prompts, processes with LLM, and saves cleaned output.
/// </summary>
public class CleanupGenerator
{
    private const string SYSTEM_PROMPT_PATH = "./prompts/tool-family-cleanup-system-prompt.txt";
    private const string USER_PROMPT_PATH = "./prompts/tool-family-cleanup-user-prompt.txt";
    private const int MIN_MAX_TOKENS = 12000; // Minimum tokens for LLM output
    private const int MAX_OUTPUT_TOKENS = 16384; // Maximum tokens supported by gpt-4o model
    private const int PROMPT_OVERHEAD_TOKENS = 1600; // Estimated tokens for system + user prompt templates (~1,590 actual)
    private const double TOKEN_MULTIPLIER = 2.0; // Output tokens = input tokens * multiplier (2x for cleanup formatting)

    // Compiled regex patterns for performance when processing multiple files
    // Using CultureInvariant for consistent behavior across different system locales
    private static readonly Regex HeadersRegex = new(@"^#{1,6}\s+.+$", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ListsRegex = new(@"^\s*[-*]\s+.+$", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LinksRegex = new(@"\[.+?\]\(.+?\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CodeBlockRegex = new(@"```markdown\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex FrontmatterRegex = new(@"^---\s*\n", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CodeFencesRegex = new(@"```[\s\S]*?```", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BlockquotesRegex = new(@"^\s*>\s+.+$", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly GenerativeAIClient _aiClient;
    private readonly CleanupConfiguration _config;
    private string? _systemPrompt;
    private string? _userPromptTemplate;

    public CleanupGenerator(GenerativeAIOptions options, CleanupConfiguration config)
    {
        _aiClient = new GenerativeAIClient(options);
        _config = config;
    }

    /// <summary>
    /// Processes all tool family markdown files in the input directory.
    /// </summary>
    public async Task ProcessAllToolFamilyFiles()
    {
        Console.WriteLine("=== Tool Family Cleanup Generation ===");
        Console.WriteLine();

        // Load prompts
        await LoadPrompts();

        // Get all tool family markdown files from input directory
        var inputDir = Path.GetFullPath(_config.InputDirectory);
        if (!Directory.Exists(inputDir))
        {
            throw new DirectoryNotFoundException($"Input directory not found: {inputDir}");
        }

        // Find all markdown files in the root of the multi-page directory (tool family files)
        // Exclude subdirectories like annotations, parameters, etc.
        var toolFamilyFiles = Directory.GetFiles(inputDir, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f)
            .ToList();

        if (toolFamilyFiles.Count == 0)
        {
            Console.WriteLine("⚠ No tool family markdown files found in input directory.");
            return;
        }

        Console.WriteLine($"Found {toolFamilyFiles.Count} tool family files to process");
        Console.WriteLine();

        // Create output directories
        var promptsDir = Path.GetFullPath(_config.PromptsOutputDirectory);
        var cleanupDir = Path.GetFullPath(_config.CleanupOutputDirectory);
        Directory.CreateDirectory(promptsDir);
        Directory.CreateDirectory(cleanupDir);

        // Process each file
        int successCount = 0;
        int failCount = 0;

        for (int i = 0; i < toolFamilyFiles.Count; i++)
        {
            var filePath = toolFamilyFiles[i];
            var fileName = Path.GetFileName(filePath);
            var progress = $"[{i + 1}/{toolFamilyFiles.Count}]";

            try
            {
                Console.WriteLine($"{progress} Processing {fileName}...");
                
                // Read the tool family file
                var content = await File.ReadAllTextAsync(filePath);
                
                // Calculate dynamic max tokens based on content size
                int maxTokens = CalculateMaxTokens(content);
                Console.WriteLine($"{progress} Content: {content.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length} words, Max tokens: {maxTokens}");
                
                // Generate prompt for this file
                var userPrompt = GenerateUserPrompt(fileName, content);
                
                // Save the prompt
                var promptFileName = Path.GetFileNameWithoutExtension(fileName) + "-prompt.txt";
                var promptPath = Path.Combine(promptsDir, promptFileName);
                await File.WriteAllTextAsync(promptPath, $"SYSTEM PROMPT:\n{_systemPrompt}\n\n---\n\nUSER PROMPT:\n{userPrompt}");
                
                // Call LLM to get cleaned markdown (using dynamic max tokens)
                string cleanedMarkdown;
                try
                {
                    cleanedMarkdown = await _aiClient.GetChatCompletionAsync(_systemPrompt!, userPrompt, maxTokens: maxTokens);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("truncated"))
                {
                    // Specific handling for truncation errors
                    Console.WriteLine($"{progress} ✗ TRUNCATED: {fileName}");
                    Console.WriteLine($"           {ex.Message}");
                    Console.WriteLine($"           Try increasing MIN_MAX_TOKENS or tokens-per-tool multiplier");
                    
                    // Log detailed error
                    var errorPath = Path.Combine(cleanupDir, Path.GetFileNameWithoutExtension(fileName) + "-truncation-error.txt");
                    await File.WriteAllTextAsync(errorPath, 
                        $"File: {fileName}\n" +
                        $"Max tokens allocated: {maxTokens}\n" +
                        $"Error: {ex.Message}\n\n" +
                        $"Solutions:\n" +
                        $"1. Check if tool count was detected (should show in console)\n" +
                        $"2. Increase MIN_MAX_TOKENS in CleanupGenerator.cs (currently {MIN_MAX_TOKENS})\n" +
                        $"3. Increase tokens per tool (currently 1000 per tool)\n" +
                        $"4. Reduce prompt sizes if they exceed 2000 words\n");
                    
                    failCount++;
                    continue;
                }
                
                // Validate that output is markdown
                if (!IsValidMarkdown(cleanedMarkdown))
                {
                    Console.WriteLine($"{progress} ⚠ Warning: LLM output may not be valid markdown for {fileName}");
                    // Log the invalid output
                    var errorPath = Path.Combine(cleanupDir, Path.GetFileNameWithoutExtension(fileName) + "-error.txt");
                    await File.WriteAllTextAsync(errorPath, $"Invalid markdown output:\n\n{cleanedMarkdown}");
                    failCount++;
                    continue;
                }
                
                // Extract markdown from response (in case LLM added explanatory text)
                var extractedMarkdown = ExtractMarkdown(cleanedMarkdown);
                
                // Save cleaned markdown
                var outputPath = Path.Combine(cleanupDir, fileName);
                await File.WriteAllTextAsync(outputPath, extractedMarkdown);
                
                Console.WriteLine($"{progress} ✓ Successfully cleaned {fileName}");
                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{progress} ✗ Failed to process {fileName}: {ex.Message}");
                failCount++;
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== Summary ===");
        Console.WriteLine($"Total files:      {toolFamilyFiles.Count}");
        Console.WriteLine($"Successful:       {successCount}");
        Console.WriteLine($"Failed:           {failCount}");
        Console.WriteLine();
        Console.WriteLine($"Prompts saved to: {promptsDir}");
        Console.WriteLine($"Cleaned files:    {cleanupDir}");
    }

    private async Task LoadPrompts()
    {
        var systemPromptPath = Path.GetFullPath(SYSTEM_PROMPT_PATH);
        var userPromptPath = Path.GetFullPath(USER_PROMPT_PATH);

        if (!File.Exists(systemPromptPath))
        {
            throw new FileNotFoundException($"System prompt not found: {systemPromptPath}");
        }

        if (!File.Exists(userPromptPath))
        {
            throw new FileNotFoundException($"User prompt template not found: {userPromptPath}");
        }

        _systemPrompt = await File.ReadAllTextAsync(systemPromptPath);
        _userPromptTemplate = await File.ReadAllTextAsync(userPromptPath);

        Console.WriteLine("✓ Prompts loaded");
        Console.WriteLine();
    }

    private string GenerateUserPrompt(string fileName, string content)
    {
        // Replace placeholders in user prompt template
        return _userPromptTemplate!
            .Replace("{{FILENAME}}", fileName)
            .Replace("{{CONTENT}}", content);
    }

    /// <summary>
    /// Calculates maximum output tokens based on input content size and tool count.
    /// Prioritizes tool count if available, falls back to word count estimation.
    /// Caps at MAX_OUTPUT_TOKENS (16,384 for gpt-4o).
    /// Formula: min(MAX_OUTPUT_TOKENS, max(MIN_MAX_TOKENS, toolCount * 1000 OR estimatedInputTokens * 2))
    /// </summary>
    private int CalculateMaxTokens(string content)
    {
        // Try to extract tool count from metadata (more accurate for large files)
        var toolCountMatch = System.Text.RegularExpressions.Regex.Match(content, @"\*\*Tool Count:\*\*\s*(\d+)");
        if (toolCountMatch.Success && int.TryParse(toolCountMatch.Groups[1].Value, out int toolCount))
        {
            // Allocate ~1000 tokens per tool (covers description, params, examples, annotations)
            // Add 2000 base tokens for frontmatter, H1, intro, Related content
            int toolBasedTokens = (toolCount * 1000) + 2000;
            int cappedTokens = Math.Min(MAX_OUTPUT_TOKENS, Math.Max(MIN_MAX_TOKENS, toolBasedTokens));
            
            Console.WriteLine($"           Token calculation: tool-count method ({toolCount} tools × 1000 + 2000)");
            if (toolBasedTokens > MAX_OUTPUT_TOKENS)
            {
                Console.WriteLine($"           ⚠ Capped at model limit: {toolBasedTokens} → {MAX_OUTPUT_TOKENS} tokens");
            }
            
            return cappedTokens;
        }
        
        // Fallback: Count words in content (less accurate but works for files without metadata)
        int wordCount = content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        
        // Estimate tokens: 1 word ≈ 1.33 tokens, so tokens ≈ wordCount / 0.75
        int estimatedInputTokens = (int)(wordCount / 0.75);
        
        // Calculate max output tokens with buffer (2x input for cleanup formatting + safety margin)
        int calculatedMaxTokens = (int)(estimatedInputTokens * TOKEN_MULTIPLIER);
        int cappedTokens = Math.Min(MAX_OUTPUT_TOKENS, Math.Max(MIN_MAX_TOKENS, calculatedMaxTokens));
        
        Console.WriteLine($"           Token calculation: word-count method ({wordCount} words × {TOKEN_MULTIPLIER} multiplier)");
        if (calculatedMaxTokens > MAX_OUTPUT_TOKENS)
        {
            Console.WriteLine($"           ⚠ Capped at model limit: {calculatedMaxTokens} → {MAX_OUTPUT_TOKENS} tokens");
        }
        
        return cappedTokens;
    }

    private bool IsValidMarkdown(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Check for common markdown patterns using compiled regex patterns
        // Validates presence of typical markdown elements to ensure LLM output is markdown
        // - Headers (# ## ###)
        // - Lists (- or *)
        // - Links ([text](url))
        // - Code fences (```)
        // - Blockquotes (>)
        
        var hasHeaders = HeadersRegex.IsMatch(text);
        var hasLists = ListsRegex.IsMatch(text);
        var hasLinks = LinksRegex.IsMatch(text);
        var hasCodeFences = CodeFencesRegex.IsMatch(text);
        var hasBlockquotes = BlockquotesRegex.IsMatch(text);
        
        // Consider it markdown if it has at least one of the common markdown patterns
        // Most tool family files will have headers and lists at minimum
        return hasHeaders || hasLists || hasLinks || hasCodeFences || hasBlockquotes;
    }

    private string ExtractMarkdown(string response)
    {
        // If the response is wrapped in markdown code blocks, extract it
        var codeBlockMatch = CodeBlockRegex.Match(response);
        if (codeBlockMatch.Success)
        {
            return codeBlockMatch.Groups[1].Value.Trim();
        }

        // If response has explanatory text before the markdown, try to extract just the markdown
        // Look for the start of typical frontmatter or first header
        var frontmatterMatch = FrontmatterRegex.Match(response);
        if (frontmatterMatch.Success)
        {
            return response.Substring(frontmatterMatch.Index).Trim();
        }

        // Return as-is if no special extraction needed
        return response.Trim();
    }
}
