// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using GenerativeAI;
using ToolFamilyCleanup.Models;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Generates metadata section (frontmatter + H1 + intro) for tool family documentation.
/// Uses hybrid approach: deterministic template for frontmatter, minimal AI for service description.
/// Issue #511: Reduces token usage from 2000-8000 → ~200 tokens.
/// </summary>
public class FamilyMetadataGenerator
{
    private const string SERVICE_DESC_SYSTEM_PROMPT = "./prompts/service-description-system-prompt.txt";
    private const string SERVICE_DESC_USER_PROMPT = "./prompts/service-description-user-prompt.txt";
    private const int MAX_TOKENS = 500; // Service description is short (~200 tokens typical)

    private readonly GenerativeAIClient _aiClient;
    private string? _systemPrompt;
    private string? _userPromptTemplate;

    public FamilyMetadataGenerator(GenerativeAIOptions options)
    {
        _aiClient = new GenerativeAIClient(options);
    }

    /// <summary>
    /// Loads prompt templates from disk (service description prompts).
    /// </summary>
    public async Task LoadPromptsAsync()
    {
        var baseDir = AppContext.BaseDirectory;
        var systemPromptPath = Path.Combine(baseDir, "prompts", "service-description-system-prompt.txt");
        var userPromptPath = Path.Combine(baseDir, "prompts", "service-description-user-prompt.txt");

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
    /// Generates metadata section for a tool family using hybrid template + AI approach.
    /// Template generates deterministic frontmatter, H1, and intro para 1.
    /// AI generates only intro para 2 (service description).
    /// </summary>
    /// <param name="familyContent">Family content with tools list</param>
    /// <param name="cliVersion">CLI version string</param>
    /// <returns>Metadata markdown (frontmatter + H1 + intro paragraphs + include)</returns>
    public async Task<string> GenerateAsync(FamilyContent familyContent, string cliVersion = "unknown")
    {
        if (_systemPrompt == null || _userPromptTemplate == null)
        {
            throw new InvalidOperationException("Prompts not loaded. Call LoadPromptsAsync() first.");
        }

        var familyDisplayName = string.IsNullOrWhiteSpace(familyContent.DisplayName)
            ? familyContent.FamilyName
            : familyContent.DisplayName;

        // Phase 1: Generate deterministic frontmatter + H1 + intro paragraph 1
        var deterministicPart = BuildDeterministicMetadata(
            familyDisplayName,
            familyContent.ToolCount,
            cliVersion,
            familyContent.Tools);

        // Phase 2: Generate intro paragraph 2 (service description) via AI
        string serviceDescription;
        try
        {
            serviceDescription = await GenerateServiceDescriptionAsync(familyDisplayName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ AI service description failed: {ex.Message}. Using fallback.");
            serviceDescription = BuildFallbackServiceDescription(familyDisplayName, familyContent.FamilyName);
        }

        // Phase 3: Assemble complete metadata
        var sb = new StringBuilder();
        sb.Append(deterministicPart);
        sb.AppendLine();
        sb.AppendLine(serviceDescription);
        sb.AppendLine();
        sb.AppendLine(MetadataConstants.IncludeParameterConsideration);

        return sb.ToString();
    }

    /// <summary>
    /// Builds deterministic frontmatter, H1, and intro paragraph 1 from templates.
    /// No AI needed - all values are derived from inputs.
    /// </summary>
    private static string BuildDeterministicMetadata(
        string displayName,
        int toolCount,
        string cliVersion,
        List<ToolContent> tools)
    {
        var title = string.Format(MetadataConstants.TitleTemplate, MetadataConstants.ProductName, displayName);
        var description = string.Format(MetadataConstants.DescriptionTemplate, MetadataConstants.ProductName, displayName);

        // Build natural language verb phrase from extracted verbs
        var verbList = GetVerbSummary(tools);
        var verbPhrase = FormatVerbsAsPhrase(verbList);

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"title: {title}");
        sb.AppendLine($"description: {description}");
        sb.AppendLine($"ms.service: {MetadataConstants.MsService}");
        sb.AppendLine($"ms.topic: {MetadataConstants.MsTopic}");
        sb.AppendLine($"tool_count: {toolCount}");
        sb.AppendLine($"mcp-cli.version: {cliVersion}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine($"The {MetadataConstants.ProductName} lets you manage {displayName} resources, including {verbPhrase}, with natural language prompts.");

        return sb.ToString();
    }

    /// <summary>
    /// Generates service description paragraph via AI.
    /// Small focused prompt (~200 tokens output).
    /// </summary>
    private async Task<string> GenerateServiceDescriptionAsync(string displayName)
    {
        var userPrompt = _userPromptTemplate!
            .Replace("{{FAMILY_DISPLAY_NAME}}", displayName);

        var response = await _aiClient.GetChatCompletionAsync(_systemPrompt!, userPrompt, maxTokens: MAX_TOKENS);
        return response.Trim();
    }

    /// <summary>
    /// Builds generic fallback service description when AI fails.
    /// </summary>
    private static string BuildFallbackServiceDescription(string displayName, string familyName)
    {
        var sanitizedName = familyName.ToLowerInvariant().Replace("-", "").Replace("_", "");
        var docPath = $"/azure/{sanitizedName}/";

        return $"{displayName} is an Azure service that provides cloud-based capabilities for your applications. " +
               $"For more information, see [{displayName} documentation]({docPath}).";
    }

    /// <summary>
    /// Formats extracted verbs as natural language phrase.
    /// Example: "create, delete, get" → "create, delete, and get"
    /// </summary>
    private static string FormatVerbsAsPhrase(string verbList)
    {
        if (string.IsNullOrWhiteSpace(verbList))
            return "operations";

        var verbs = verbList.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .ToList();

        if (verbs.Count == 0)
            return "operations";

        if (verbs.Count == 1)
            return verbs[0];

        if (verbs.Count == 2)
            return $"{verbs[0]} and {verbs[1]}";

        // 3+ verbs: "create, delete, and get"
        var allButLast = string.Join(", ", verbs.Take(verbs.Count - 1));
        return $"{allButLast}, and {verbs.Last()}";
    }

    /// <summary>
    /// Extracts deduplicated, sorted verbs from tool commands (last segment of each command).
    /// Returns a comma-separated string like "create, delete, get, list, update".
    /// </summary>
    public static string GetVerbSummary(List<ToolContent> tools)
    {
        var verbs = tools
            .Select(t => t.Command?.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault())
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!.ToLowerInvariant())
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        return string.Join(", ", verbs);
    }
}
