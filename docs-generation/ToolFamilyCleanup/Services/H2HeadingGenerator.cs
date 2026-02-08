// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using ToolFamilyCleanup.Models;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Generates AI-improved H2 headings for individual tools based on their MCP command and description.
/// Uses Azure OpenAI to create action-oriented, concise headings following Microsoft style guides.
/// </summary>
public class H2HeadingGenerator
{
    private readonly GenerativeAIClient _aiClient;
    private string? _systemPrompt;
    private string? _userPromptTemplate;

    public H2HeadingGenerator(GenerativeAIOptions options)
    {
        _aiClient = new GenerativeAIClient(options);
    }

    /// <summary>
    /// Loads system and user prompt templates from embedded resources or files.
    /// </summary>
    public async Task LoadPromptsAsync()
    {
        var baseDir = AppContext.BaseDirectory;
        var systemPromptPath = Path.Combine(baseDir, "prompts", "h2-heading-system-prompt.txt");
        var userPromptPath = Path.Combine(baseDir, "prompts", "h2-heading-user-prompt.txt");

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
    /// Generates an improved H2 heading for a single tool using AI.
    /// </summary>
    /// <param name="tool">Tool content with command, description, and family name</param>
    /// <param name="familyDisplayName">Display name of the tool family (for context)</param>
    /// <returns>Generated H2 heading text (without markdown syntax)</returns>
    public async Task<string> GenerateHeadingAsync(ToolContent tool, string familyDisplayName)
    {
        if (string.IsNullOrWhiteSpace(tool.Command) || string.IsNullOrWhiteSpace(tool.Description))
        {
            // Fallback: use the tool's existing heading if we don't have enough data
            return ExtractHeadingFromContent(tool.Content) ?? tool.ToolName ?? "Tool";
        }

        var userPrompt = GenerateUserPrompt(tool, familyDisplayName);
        
        try
        {
            var heading = await _aiClient.GetChatCompletionAsync(_systemPrompt!, userPrompt, maxTokens: 100);
            return CleanHeading(ExtractMarkdownFromResponse(heading));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Failed to generate H2 heading for '{tool.Command}': {ex.Message}");
            return ExtractHeadingFromContent(tool.Content) ?? tool.ToolName ?? "Tool";
        }
    }

    private string GenerateUserPrompt(ToolContent tool, string familyDisplayName)
    {
        return _userPromptTemplate!
            .Replace("{{COMMAND}}", tool.Command ?? "")
            .Replace("{{DESCRIPTION}}", tool.Description ?? "")
            .Replace("{{FAMILY_NAME}}", familyDisplayName ?? "");
    }

    /// <summary>
    /// Cleans and validates the AI-generated heading.
    /// Removes extra whitespace and markdown syntax if present.
    /// Ensures proper H2 markdown syntax (##).
    /// </summary>
    private string CleanHeading(string heading)
    {
        if (string.IsNullOrWhiteSpace(heading))
        {
            return "## Tool";
        }

        var cleaned = heading.Trim();
        
        // Remove markdown syntax if AI accidentally included it
        if (cleaned.StartsWith("##"))
        {
            cleaned = cleaned.Substring(2).Trim();
        }
        else if (cleaned.StartsWith("#"))
        {
            cleaned = cleaned.Substring(1).Trim();
        }

        // Remove backticks if present
        cleaned = cleaned.Replace("`", "").Trim();

        // Return with H2 markdown syntax
        return $"## {cleaned}";
    }

    /// <summary>
    /// Extracts an existing H2 heading from tool content (fallback).
    /// </summary>
    private static string? ExtractHeadingFromContent(string content)
    {
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("## "))
            {
                return line.Substring(3).Trim();
            }
        }
        return null;
    }

    /// <summary>
    /// Extracts markdown from AI response if wrapped in code fences.
    /// </summary>
    private static string ExtractMarkdownFromResponse(string response)
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
