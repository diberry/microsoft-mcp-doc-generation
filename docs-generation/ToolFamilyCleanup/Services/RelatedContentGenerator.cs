// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using ToolFamilyCleanup.Models;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Generates "Related content" section for tool family documentation using AI.
/// </summary>
public class RelatedContentGenerator
{
    private const string SYSTEM_PROMPT_PATH = "./prompts/related-content-system-prompt.txt";
    private const string USER_PROMPT_PATH = "./prompts/related-content-user-prompt.txt";
    private const int MAX_TOKENS = 1000; // Related content is small

    private readonly GenerativeAIClient _aiClient;
    private string? _systemPrompt;
    private string? _userPromptTemplate;

    public RelatedContentGenerator(GenerativeAIOptions options)
    {
        _aiClient = new GenerativeAIClient(options);
    }

    /// <summary>
    /// Loads prompt templates from disk.
    /// </summary>
    public async Task LoadPromptsAsync()
    {
        var baseDir = AppContext.BaseDirectory;
        var systemPromptPath = Path.Combine(baseDir, "prompts", "related-content-system-prompt.txt");
        var userPromptPath = Path.Combine(baseDir, "prompts", "related-content-user-prompt.txt");

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
    /// Generates related content section for a tool family.
    /// </summary>
    /// <param name="familyContent">Family content with tools list</param>
    /// <returns>Related content markdown (## Related content + links)</returns>
    public async Task<string> GenerateAsync(FamilyContent familyContent)
    {
        if (_systemPrompt == null || _userPromptTemplate == null)
        {
            throw new InvalidOperationException("Prompts not loaded. Call LoadPromptsAsync() first.");
        }

        // Generate user prompt with placeholders replaced
        var userPrompt = _userPromptTemplate
            .Replace("{{FAMILY_NAME}}", familyContent.FamilyName)
            .Replace("{{TOOL_COUNT}}", familyContent.ToolCount.ToString())
            .Replace("{{TOOL_LIST}}", familyContent.ToolNamesList);

        // Call LLM
        var relatedContent = await _aiClient.GetChatCompletionAsync(_systemPrompt, userPrompt, maxTokens: MAX_TOKENS);

        return relatedContent.Trim();
    }
}
