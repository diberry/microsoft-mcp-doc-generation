// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using ToolFamilyCleanup.Models;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Generates metadata section (frontmatter + H1 + intro) for tool family documentation using AI.
/// </summary>
public class FamilyMetadataGenerator
{
    private const string SYSTEM_PROMPT_PATH = "./prompts/family-metadata-system-prompt.txt";
    private const string USER_PROMPT_PATH = "./prompts/family-metadata-user-prompt.txt";
    private const int MAX_TOKENS = 2000; // Metadata is typically small

    private readonly GenerativeAIClient _aiClient;
    private string? _systemPrompt;
    private string? _userPromptTemplate;

    public FamilyMetadataGenerator(GenerativeAIOptions options)
    {
        _aiClient = new GenerativeAIClient(options);
    }

    /// <summary>
    /// Loads prompt templates from disk.
    /// </summary>
    public async Task LoadPromptsAsync()
    {
        var baseDir = AppContext.BaseDirectory;
        var systemPromptPath = Path.Combine(baseDir, "prompts", "family-metadata-system-prompt.txt");
        var userPromptPath = Path.Combine(baseDir, "prompts", "family-metadata-user-prompt.txt");

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
    }

    /// <summary>
    /// Generates metadata section for a tool family.
    /// </summary>
    /// <param name="familyContent">Family content with tools list</param>
    /// <param name="cliVersion">CLI version string</param>
    /// <returns>Metadata markdown (frontmatter + H1 + intro)</returns>
    public async Task<string> GenerateAsync(FamilyContent familyContent, string cliVersion = "unknown")
    {
        if (_systemPrompt == null || _userPromptTemplate == null)
        {
            throw new InvalidOperationException("Prompts not loaded. Call LoadPromptsAsync() first.");
        }

        var familyDisplayName = string.IsNullOrWhiteSpace(familyContent.DisplayName)
            ? familyContent.FamilyName
            : familyContent.DisplayName;

        // Generate user prompt with placeholders replaced
        var userPrompt = _userPromptTemplate
            .Replace("{{FAMILY_NAME}}", familyDisplayName)
            .Replace("{{TOOL_COUNT}}", familyContent.ToolCount.ToString())
            .Replace("{{CLI_VERSION}}", cliVersion)
            .Replace("{{TOOL_LIST}}", familyContent.ToolNamesList);

        // Call LLM
        var metadata = await _aiClient.GetChatCompletionAsync(_systemPrompt, userPrompt, maxTokens: MAX_TOKENS);

        // Extract markdown if wrapped in code fences
        return ExtractMarkdown(metadata.Trim());
    }

    /// <summary>
    /// Extracts markdown from AI response if wrapped in code fences.
    /// </summary>
    private static string ExtractMarkdown(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        // Check for markdown code fences
        var match = System.Text.RegularExpressions.Regex.Match(response, @"```(?:markdown)?\s*(.*?)\s*```", System.Text.RegularExpressions.RegexOptions.Singleline);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return response.Trim();
    }
}
