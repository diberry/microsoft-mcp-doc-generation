// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using System.Text.RegularExpressions;
using ToolFamilyCleanup.Models;
using System.Text.Json;

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
    private readonly GenerativeAIOptions _options;
    private string? _systemPrompt;
    private string? _userPromptTemplate;

    private sealed class BrandMapping
    {
        public string? mcpServerName { get; set; }
        public string? brandName { get; set; }
    }

    public CleanupGenerator(GenerativeAIOptions options, CleanupConfiguration config)
    {
        _options = options;
        _aiClient = new GenerativeAIClient(options);
        _config = config;
    }

    private static Dictionary<string, string> LoadBrandMappings()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mappingPath = Path.Combine(Directory.GetCurrentDirectory(), "brand-to-server-mapping.json");
        if (!File.Exists(mappingPath))
        {
            return result;
        }

        try
        {
            var json = File.ReadAllText(mappingPath);
            var mappings = JsonSerializer.Deserialize<List<BrandMapping>>(json);
            if (mappings == null)
            {
                return result;
            }

            foreach (var mapping in mappings)
            {
                if (!string.IsNullOrWhiteSpace(mapping.mcpServerName) &&
                    !string.IsNullOrWhiteSpace(mapping.brandName))
                {
                    result[mapping.mcpServerName.Trim().ToLowerInvariant()] = mapping.brandName.Trim();
                }
            }
        }
        catch
        {
            return result;
        }

        return result;
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
        var baseDir = AppContext.BaseDirectory;
        var systemPromptPath = Path.Combine(baseDir, "prompts", "tool-family-cleanup-system-prompt.txt");
        var userPromptPath = Path.Combine(baseDir, "prompts", "tool-family-cleanup-user-prompt.txt");

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
            int cappedToolTokens = Math.Min(MAX_OUTPUT_TOKENS, Math.Max(MIN_MAX_TOKENS, toolBasedTokens));
            
            Console.WriteLine($"           Token calculation: tool-count method ({toolCount} tools × 1000 + 2000)");
            if (toolBasedTokens > MAX_OUTPUT_TOKENS)
            {
                Console.WriteLine($"           ⚠ Capped at model limit: {toolBasedTokens} → {MAX_OUTPUT_TOKENS} tokens");
            }
            
            return cappedToolTokens;
        }
        
        // Fallback: Count words in content (less accurate but works for files without metadata)
        int wordCount = content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        
        // Estimate tokens: 1 word ≈ 1.33 tokens, so tokens ≈ wordCount / 0.75
        int estimatedInputTokens = (int)(wordCount / 0.75);
        
        // Calculate max output tokens with buffer (2x input for cleanup formatting + safety margin)
        int calculatedMaxTokens = (int)(estimatedInputTokens * TOKEN_MULTIPLIER);
        int cappedWordTokens = Math.Min(MAX_OUTPUT_TOKENS, Math.Max(MIN_MAX_TOKENS, calculatedMaxTokens));
        
        Console.WriteLine($"           Token calculation: word-count method ({wordCount} words × {TOKEN_MULTIPLIER} multiplier)");
        if (calculatedMaxTokens > MAX_OUTPUT_TOKENS)
        {
            Console.WriteLine($"           ⚠ Capped at model limit: {calculatedMaxTokens} → {MAX_OUTPUT_TOKENS} tokens");
        }
        
        return cappedWordTokens;
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

    /// <summary>
    /// Processes tool families using multi-phase approach: read tools, generate metadata/related content, stitch together.
    /// Solves 16K token limit by processing tools individually and assembling at the end.
    /// </summary>
    public async Task ProcessToolFamiliesMultiPhase()
    {
        Console.WriteLine("=== Tool Family Cleanup Generation (Multi-Phase) ===");
        Console.WriteLine();

        // Create output directories (resolve relative paths)
        var metadataDir = Path.GetFullPath(_config.MetadataOutputDirectory);
        var relatedDir = Path.GetFullPath(_config.RelatedContentOutputDirectory);
        var outputDir = Path.GetFullPath(_config.MultiFileOutputDirectory);
        Directory.CreateDirectory(metadataDir);
        Directory.CreateDirectory(relatedDir);
        Directory.CreateDirectory(outputDir);

        // Phase 1: Read and group tools by family (resolve relative path)
        Console.WriteLine("Phase 1: Reading tools from directory...");
        var toolsInputDir = Path.GetFullPath(_config.ToolsInputDirectory);
        var toolReader = new ToolReader(toolsInputDir);
        var toolsByFamily = await toolReader.ReadAndGroupToolsAsync();

        if (toolsByFamily.Count == 0)
        {
            Console.WriteLine("⚠ No tool families found.");
            return;
        }

        // Initialize generators
        var metadataGenerator = new FamilyMetadataGenerator(_options);
        var relatedContentGenerator = new RelatedContentGenerator(_options);
        var stitcher = new FamilyFileStitcher();

        await metadataGenerator.LoadPromptsAsync();
        await relatedContentGenerator.LoadPromptsAsync();
        Console.WriteLine("✓ Generators initialized");
        Console.WriteLine();

        var brandMappings = LoadBrandMappings();

        // Phase 2 & 3 & 4: Process each family
        int successCount = 0;
        int failCount = 0;
        int totalTokensUsed = 0;

        foreach (var (familyName, tools) in toolsByFamily.OrderBy(kv => kv.Key))
        {
            var progress = $"[{successCount + failCount + 1}/{toolsByFamily.Count}]";

            try
            {
                Console.WriteLine($"{progress} Processing family: {familyName} ({tools.Count} tools)...");

                // Create FamilyContent object
                var displayName = brandMappings.TryGetValue(familyName, out var brandName)
                    ? brandName
                    : familyName;

                var familyContent = new FamilyContent
                {
                    FamilyName = familyName,
                    DisplayName = displayName,
                    Tools = tools,
                    Metadata = string.Empty, // Will be populated
                    RelatedContent = string.Empty // Will be populated
                };

                // Phase 2: Generate metadata
                Console.Write($"{progress}   Phase 2: Generating metadata... ");
                var metadata = await metadataGenerator.GenerateAsync(familyContent);
                familyContent.Metadata = metadata;
                
                // Save metadata
                var metadataPath = Path.Combine(metadataDir, $"{familyName}-metadata.md");
                await File.WriteAllTextAsync(metadataPath, metadata);
                Console.WriteLine($"✓ ({EstimateTokens(metadata)} tokens)");

                // Phase 3: Generate related content
                Console.Write($"{progress}   Phase 3: Generating related content... ");
                var relatedContent = await relatedContentGenerator.GenerateAsync(familyContent);
                familyContent.RelatedContent = relatedContent;
                
                // Save related content
                var relatedPath = Path.Combine(relatedDir, $"{familyName}-related.md");
                await File.WriteAllTextAsync(relatedPath, relatedContent);
                Console.WriteLine($"✓ ({EstimateTokens(relatedContent)} tokens)");

                // Phase 4: Stitch together
                Console.Write($"{progress}   Phase 4: Stitching file... ");
                var outputPath = Path.Combine(outputDir, $"{familyName}.md");
                await stitcher.StitchAndSaveAsync(familyContent, outputPath);
                Console.WriteLine($"✓ Saved to {familyName}.md");

                totalTokensUsed += EstimateTokens(metadata) + EstimateTokens(relatedContent);
                successCount++;
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{progress} ✗ Failed to process {familyName}: {ex.Message}");
                failCount++;
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== Summary ===");
        Console.WriteLine($"Total families:   {toolsByFamily.Count}");
        Console.WriteLine($"Successful:       {successCount}");
        Console.WriteLine($"Failed:           {failCount}");
        Console.WriteLine($"Est. AI tokens:   ~{totalTokensUsed:N0}");
        Console.WriteLine();
        Console.WriteLine($"Metadata:         {metadataDir}");
        Console.WriteLine($"Related content:  {relatedDir}");
        Console.WriteLine($"Final files:      {outputDir}");
    }

    /// <summary>
    /// Estimates token count from text (rough approximation: 1 token ≈ 0.75 words).
    /// </summary>
    private int EstimateTokens(string text)
    {
        var wordCount = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        return (int)(wordCount / 0.75);
    }
}
