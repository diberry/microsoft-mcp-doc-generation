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

    // Compiled regex patterns for performance when processing multiple files
    private static readonly Regex HeadersRegex = new(@"^#{1,6}\s+.+$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex ListsRegex = new(@"^\s*[-*]\s+.+$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex LinksRegex = new(@"\[.+?\]\(.+?\)", RegexOptions.Compiled);
    private static readonly Regex CodeBlockRegex = new(@"```markdown\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex FrontmatterRegex = new(@"^---\s*\n", RegexOptions.Multiline | RegexOptions.Compiled);

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
                
                // Generate prompt for this file
                var userPrompt = GenerateUserPrompt(fileName, content);
                
                // Save the prompt
                var promptFileName = Path.GetFileNameWithoutExtension(fileName) + "-prompt.txt";
                var promptPath = Path.Combine(promptsDir, promptFileName);
                await File.WriteAllTextAsync(promptPath, $"SYSTEM PROMPT:\n{_systemPrompt}\n\n---\n\nUSER PROMPT:\n{userPrompt}");
                
                // Call LLM to get cleaned markdown
                var cleanedMarkdown = await _aiClient.GetChatCompletionAsync(_systemPrompt!, userPrompt, maxTokens: 16000);
                
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

    private bool IsValidMarkdown(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Check for common markdown patterns using compiled regex patterns
        // - Headers (# ## ###)
        // - Links ([text](url))
        // - Lists (- or *)
        // - Code blocks (```)
        
        var hasHeaders = HeadersRegex.IsMatch(text);
        var hasLists = ListsRegex.IsMatch(text);
        var hasLinks = LinksRegex.IsMatch(text);
        
        // Consider it markdown if it has at least headers or lists
        return hasHeaders || hasLists || hasLinks;
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
