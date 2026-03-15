// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Azure.Mcp.TextTransformation.Services;
using HorizontalArticleGenerator.Models;

namespace HorizontalArticleGenerator.Generators;

/// <summary>
/// Processes AI-generated article content: validates, auto-corrects, and transforms.
/// Extracted from HorizontalArticleGenerator for testability.
/// </summary>
public class ArticleContentProcessor
{
    private readonly TransformationEngine? _transformationEngine;

    public ArticleContentProcessor(TransformationEngine? transformationEngine = null)
    {
        _transformationEngine = transformationEngine;
    }

    /// <summary>
    /// Results from validation: corrections applied and warnings raised.
    /// </summary>
    public class ValidationResult
    {
        public List<string> Corrections { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<string> CriticalErrors { get; } = new();
        public bool HasCriticalErrors => CriticalErrors.Count > 0;
    }

    /// <summary>
    /// Validate and auto-correct AI-generated content for common quality issues.
    /// Mutates the input data in place.
    /// </summary>
    public ValidationResult Validate(AIGeneratedArticleData aiData, string serviceName)
    {
        var result = new ValidationResult();

        StripTrailingPeriods(aiData, result);
        FixBrokenSentences(aiData, result);
        FixRedundantWords(aiData, result);
        ValidateLinkUrls(aiData, result);
        DeduplicateAdditionalLinks(aiData, result);
        ValidateRbacRoles(aiData, result, serviceName);
        ValidateToolDescriptions(aiData, result);
        ValidateBestPracticeCount(aiData, result);
        ValidateCapabilityToolRatio(aiData, result);

        return result;
    }

    /// <summary>
    /// Apply text transformations (static text replacements) to AI-generated content.
    /// Uses TransformText (no trailing period) for titles and mid-sentence fields.
    /// Uses TransformDescription (with trailing period) for full sentences.
    /// </summary>
    public void ApplyTransformations(AIGeneratedArticleData aiData)
    {
        if (_transformationEngine == null)
            return;

        // ServiceShortDescription is interpolated mid-sentence — must NOT end with a period
        aiData.ServiceShortDescription = _transformationEngine.TransformText(aiData.ServiceShortDescription);
        aiData.ServiceOverview = _transformationEngine.TransformDescription(aiData.ServiceOverview);

        // Capabilities — rendered as bullet items, must NOT end with periods
        if (aiData.Capabilities != null)
        {
            for (int i = 0; i < aiData.Capabilities.Count; i++)
            {
                aiData.Capabilities[i] = _transformationEngine.TransformText(aiData.Capabilities[i]);
            }
        }

        // Prerequisites — descriptions are full sentences
        if (aiData.ServiceSpecificPrerequisites != null)
        {
            foreach (var prereq in aiData.ServiceSpecificPrerequisites)
            {
                prereq.Description = _transformationEngine.TransformDescription(prereq.Description);
            }
        }

        // Tool descriptions are full sentences  
        if (aiData.Tools != null)
        {
            foreach (var tool in aiData.Tools)
            {
                tool.ShortDescription = _transformationEngine.TransformDescription(tool.ShortDescription);
            }
        }

        // Scenarios — titles must NOT end with periods
        if (aiData.Scenarios != null)
        {
            foreach (var scenario in aiData.Scenarios)
            {
                scenario.Title = _transformationEngine.TransformText(scenario.Title);
                scenario.Description = _transformationEngine.TransformDescription(scenario.Description);
                scenario.ExpectedOutcome = _transformationEngine.TransformDescription(scenario.ExpectedOutcome);
                if (scenario.Examples != null)
                {
                    for (int i = 0; i < scenario.Examples.Count; i++)
                    {
                        scenario.Examples[i] = _transformationEngine.TransformDescription(scenario.Examples[i]);
                    }
                }
            }
        }

        // AI-specific scenarios — titles must NOT end with periods
        if (aiData.AISpecificScenarios != null)
        {
            foreach (var scenario in aiData.AISpecificScenarios)
            {
                scenario.Title = _transformationEngine.TransformText(scenario.Title);
                scenario.Description = _transformationEngine.TransformDescription(scenario.Description);
                if (scenario.Examples != null)
                {
                    for (int i = 0; i < scenario.Examples.Count; i++)
                    {
                        scenario.Examples[i] = _transformationEngine.TransformDescription(scenario.Examples[i]);
                    }
                }
            }
        }

        // Best practices — titles must NOT end with periods
        if (aiData.BestPractices != null)
        {
            foreach (var practice in aiData.BestPractices)
            {
                practice.Title = _transformationEngine.TransformText(practice.Title);
                practice.Description = _transformationEngine.TransformDescription(practice.Description);
            }
        }

        // Common issues — titles must NOT end with periods
        if (aiData.CommonIssues != null)
        {
            foreach (var issue in aiData.CommonIssues)
            {
                issue.Title = _transformationEngine.TransformText(issue.Title);
                issue.Description = _transformationEngine.TransformDescription(issue.Description);
                issue.Resolution = _transformationEngine.TransformDescription(issue.Resolution);
            }
        }

        // Required roles — purposes are full sentences
        if (aiData.RequiredRoles != null)
        {
            foreach (var role in aiData.RequiredRoles)
            {
                role.Purpose = _transformationEngine.TransformDescription(role.Purpose);
            }
        }

        // Authentication notes are full sentences
        if (!string.IsNullOrEmpty(aiData.AuthenticationNotes))
        {
            aiData.AuthenticationNotes = _transformationEngine.TransformDescription(aiData.AuthenticationNotes);
        }
    }

    /// <summary>
    /// Run the full processing pipeline: validate then transform.
    /// This is the order used by the generator.
    /// </summary>
    public ValidationResult Process(AIGeneratedArticleData aiData, string serviceName)
    {
        var result = Validate(aiData, serviceName);
        ApplyTransformations(aiData);
        return result;
    }

    // ===== Private validation methods =====

    private static void StripTrailingPeriods(AIGeneratedArticleData aiData, ValidationResult result)
    {
        if (!string.IsNullOrEmpty(aiData.ServiceShortDescription))
        {
            var trimmed = aiData.ServiceShortDescription.TrimEnd('.', ' ');
            if (trimmed != aiData.ServiceShortDescription)
            {
                aiData.ServiceShortDescription = trimmed;
                result.Corrections.Add("Stripped trailing period from serviceShortDescription");
            }
        }

        if (aiData.Capabilities != null)
        {
            for (int i = 0; i < aiData.Capabilities.Count; i++)
            {
                var trimmed = aiData.Capabilities[i].TrimEnd('.', ' ');
                if (trimmed != aiData.Capabilities[i])
                {
                    aiData.Capabilities[i] = trimmed;
                    result.Corrections.Add($"Stripped trailing period from capability: '{trimmed}'");
                }
            }
        }

        if (aiData.BestPractices != null)
        {
            foreach (var bp in aiData.BestPractices)
            {
                var trimmed = bp.Title.TrimEnd('.', ' ');
                if (trimmed != bp.Title)
                {
                    bp.Title = trimmed;
                    result.Corrections.Add($"Stripped trailing period from best practice title: '{trimmed}'");
                }
            }
        }

        if (aiData.ServiceSpecificPrerequisites != null)
        {
            foreach (var prereq in aiData.ServiceSpecificPrerequisites)
            {
                var trimmed = prereq.Title.TrimEnd('.', ' ');
                if (trimmed != prereq.Title)
                {
                    prereq.Title = trimmed;
                    result.Corrections.Add($"Stripped trailing period from prerequisite title: '{trimmed}'");
                }
            }
        }

        if (aiData.Scenarios != null)
        {
            foreach (var scenario in aiData.Scenarios)
            {
                var trimmed = scenario.Title.TrimEnd('.', ' ');
                if (trimmed != scenario.Title)
                {
                    scenario.Title = trimmed;
                    result.Corrections.Add($"Stripped trailing period from scenario title: '{trimmed}'");
                }
            }
        }
    }

    private static void FixBrokenSentences(AIGeneratedArticleData aiData, ValidationResult result)
    {
        if (!string.IsNullOrEmpty(aiData.ServiceShortDescription))
        {
            var before = aiData.ServiceShortDescription;
            var after = Regex.Replace(before, @"\. ([a-z])", " $1");
            if (after != before)
            {
                aiData.ServiceShortDescription = after;
                result.Corrections.Add("Fixed grammar in serviceShortDescription");
            }
        }

        if (!string.IsNullOrEmpty(aiData.ServiceOverview))
        {
            var before = aiData.ServiceOverview;
            var after = Regex.Replace(before, @"\. ([a-z])", " $1");
            if (after != before)
            {
                aiData.ServiceOverview = after;
                result.Corrections.Add("Fixed grammar in serviceOverview");
            }
        }

        if (!string.IsNullOrEmpty(aiData.AuthenticationNotes))
        {
            var before = aiData.AuthenticationNotes;
            var after = Regex.Replace(before, @"\. ([a-z])", " $1");
            if (after != before)
            {
                aiData.AuthenticationNotes = after;
                result.Corrections.Add("Fixed grammar in authenticationNotes");
            }
        }
    }

    private static void FixRedundantWords(AIGeneratedArticleData aiData, ValidationResult result)
    {
        if (!string.IsNullOrEmpty(aiData.ServiceOverview))
        {
            var words = aiData.ServiceOverview.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2 && words[0].Equals(words[1], StringComparison.OrdinalIgnoreCase))
            {
                aiData.ServiceOverview = string.Join(" ", words.Skip(1));
                result.Corrections.Add($"Removed redundant word at start: '{words[0]}'");
            }
        }
    }

    private static void ValidateRbacRoles(AIGeneratedArticleData aiData, ValidationResult result, string serviceName)
    {
        var suspiciousPatterns = new[] { "Knowledge Base Data", "Feature Data", "Resource Data", "KB Data" };
        var knownAzureRoleKeywords = new[]
        {
            "Contributor", "Reader", "Owner", "User Access Administrator",
            "Data Reader", "Data Contributor", "Data Owner",
            "Index Data Reader", "Index Data Contributor", "Service Contributor",
            "Secrets User", "Secrets Officer", "Crypto Officer"
        };

        // "Administrator" is never used in Azure built-in RBAC roles.
        // Azure uses: Contributor, Reader, Owner, User, Operator.
        const string invalidSuffix = "Administrator";

        // Generic prefixes that aren't real Azure service qualifiers.
        // Real roles use specific service names (e.g., "SQL DB", "Storage Blob", "Key Vault").
        var genericPrefixes = new[] { "Database", "Application", "Resource" };

        if (aiData.RequiredRoles == null) return;

        foreach (var role in aiData.RequiredRoles)
        {
            // Check for "Administrator" suffix — not a valid Azure RBAC action
            if (role.Name.EndsWith(invalidSuffix, StringComparison.OrdinalIgnoreCase))
            {
                result.CriticalErrors.Add(
                    $"INVENTED RBAC ROLE: '{role.Name}' — Azure RBAC roles never use 'Administrator'. " +
                    "Use 'Contributor', 'Reader', 'Owner', 'User', or 'Operator' instead");
                continue;
            }

            // Check for overly generic prefix (e.g., "Database Contributor" instead of "SQL DB Contributor")
            var words = role.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 2 && genericPrefixes.Any(p => words[0].Equals(p, StringComparison.OrdinalIgnoreCase)))
            {
                result.CriticalErrors.Add(
                    $"INVENTED RBAC ROLE: '{role.Name}' — too generic. " +
                    "Use a specific Azure service prefix (e.g., 'SQL DB Contributor', 'Cosmos DB Operator')");
                continue;
            }

            bool hasKnownKeyword = knownAzureRoleKeywords.Any(kw => role.Name.Contains(kw, StringComparison.OrdinalIgnoreCase));
            bool hasSuspiciousPattern = suspiciousPatterns.Any(sp => role.Name.Contains(sp, StringComparison.OrdinalIgnoreCase));

            if (hasSuspiciousPattern && !hasKnownKeyword)
            {
                result.CriticalErrors.Add($"INVENTED RBAC ROLE: '{role.Name}'");
            }
        }
    }

    private static void ValidateToolDescriptions(AIGeneratedArticleData aiData, ValidationResult result)
    {
        if (aiData.Tools == null) return;

        foreach (var tool in aiData.Tools)
        {
            var wordCount = tool.ShortDescription.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (wordCount < 6)
            {
                result.Warnings.Add($"Tool '{tool.Command}' description too short: {wordCount} words (target: 8-12)");
            }
            if (tool.ShortDescription.Contains("get details", StringComparison.OrdinalIgnoreCase) ||
                tool.ShortDescription.Contains("get information", StringComparison.OrdinalIgnoreCase))
            {
                result.Warnings.Add($"Tool '{tool.Command}' has generic description: '{tool.ShortDescription}'");
            }
        }
    }

    private static void ValidateBestPracticeCount(AIGeneratedArticleData aiData, ValidationResult result)
    {
        if (aiData.BestPractices == null || aiData.BestPractices.Count < 3)
        {
            result.Warnings.Add($"Only {aiData.BestPractices?.Count ?? 0} best practices (minimum 3 required)");
        }
    }

    /// <summary>
    /// Strip learn.microsoft.com prefix from URLs and remove links with fabricated URL patterns.
    /// </summary>
    private static void ValidateLinkUrls(AIGeneratedArticleData aiData, ValidationResult result)
    {
        // Strip learn.microsoft.com prefix from serviceDocLink
        if (!string.IsNullOrEmpty(aiData.ServiceDocLink))
        {
            var cleaned = StripLearnPrefix(aiData.ServiceDocLink);
            if (cleaned != aiData.ServiceDocLink)
            {
                aiData.ServiceDocLink = cleaned;
                result.Corrections.Add("Stripped learn.microsoft.com prefix from serviceDocLink");
            }
        }

        if (aiData.AdditionalLinks == null || aiData.AdditionalLinks.Count == 0) return;

        // Strip prefixes from all additional links
        foreach (var link in aiData.AdditionalLinks)
        {
            var cleaned = StripLearnPrefix(link.Url);
            if (cleaned != link.Url)
            {
                link.Url = cleaned;
                result.Corrections.Add($"Stripped learn.microsoft.com prefix from link: '{link.Title}'");
            }
        }

        // Remove links with empty URLs (AI was instructed to leave URL empty when uncertain)
        var emptyLinks = aiData.AdditionalLinks
            .Where(link => string.IsNullOrWhiteSpace(link.Url))
            .ToList();

        foreach (var link in emptyLinks)
        {
            aiData.AdditionalLinks.Remove(link);
            result.Corrections.Add($"Removed link with empty URL: '{link.Title}'");
        }

        // Remove links with fabricated URL patterns (e.g., /azure/service/docs)
        var fabricatedLinks = aiData.AdditionalLinks
            .Where(link => link.Url.TrimEnd('/').EndsWith("/docs", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var link in fabricatedLinks)
        {
            aiData.AdditionalLinks.Remove(link);
            result.Corrections.Add($"Removed link with fabricated URL pattern: '{link.Title}' ({link.Url})");
        }
    }

    /// <summary>
    /// Remove additional links that duplicate the service doc link already generated by the template.
    /// The template renders: [ServiceName documentation](serviceDocLink)
    /// Additional links with the same service path and a generic "documentation" title are duplicates.
    /// </summary>
    private static void DeduplicateAdditionalLinks(AIGeneratedArticleData aiData, ValidationResult result)
    {
        if (aiData.AdditionalLinks == null || aiData.AdditionalLinks.Count == 0 || string.IsNullOrEmpty(aiData.ServiceDocLink))
            return;

        var serviceBasePath = ExtractServiceBasePath(aiData.ServiceDocLink);

        var duplicates = aiData.AdditionalLinks.Where(link =>
        {
            // Exact URL match (ignoring trailing slash)
            if (string.Equals(link.Url.TrimEnd('/'), aiData.ServiceDocLink.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                return true;

            // Same service area + generic title
            if (serviceBasePath != null)
            {
                var linkBasePath = ExtractServiceBasePath(link.Url);
                if (string.Equals(linkBasePath, serviceBasePath, StringComparison.OrdinalIgnoreCase) &&
                    (link.Title.Contains("documentation", StringComparison.OrdinalIgnoreCase) ||
                     link.Title.EndsWith("overview", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }).ToList();

        foreach (var link in duplicates)
        {
            aiData.AdditionalLinks.Remove(link);
            result.Corrections.Add($"Removed duplicate additional link: '{link.Title}' ({link.Url})");
        }
    }

    /// <summary>
    /// Warn when capabilities significantly outnumber available tools,
    /// which suggests fabricated capabilities beyond what tools support.
    /// </summary>
    private static void ValidateCapabilityToolRatio(AIGeneratedArticleData aiData, ValidationResult result)
    {
        if (aiData.Capabilities == null || aiData.Tools == null) return;

        var toolCount = aiData.Tools.Count;
        var capCount = aiData.Capabilities.Count;

        if (toolCount == 0) return;

        // Capabilities should map 1:1 to tools.
        // For single-tool services, more than 1 capability is suspicious.
        // For multi-tool services, more than tool count is suspicious.
        var maxReasonable = toolCount;

        if (capCount > maxReasonable)
        {
            result.Warnings.Add($"Capabilities ({capCount}) exceed tool count ({toolCount}). " +
                $"Each capability should map 1:1 to a tool description. Some capabilities might be fabricated.");
        }
    }

    /// <summary>
    /// Extract the first two path segments from an Azure docs URL.
    /// E.g., "/azure/app-service/overview" → "/azure/app-service"
    /// </summary>
    private static string? ExtractServiceBasePath(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        var segments = url.TrimStart('/').Split('/');
        return segments.Length >= 2 ? $"/{segments[0]}/{segments[1]}" : null;
    }

    private static string StripLearnPrefix(string url)
    {
        const string prefix1 = "https://learn.microsoft.com/en-us";
        const string prefix2 = "https://learn.microsoft.com";

        if (url.StartsWith(prefix1, StringComparison.OrdinalIgnoreCase))
            return url[prefix1.Length..];
        if (url.StartsWith(prefix2, StringComparison.OrdinalIgnoreCase))
            return url[prefix2.Length..];
        return url;
    }
}
