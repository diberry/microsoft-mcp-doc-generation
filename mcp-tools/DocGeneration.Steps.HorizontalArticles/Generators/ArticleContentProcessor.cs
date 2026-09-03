// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Mcp.TextTransformation.Services;
using HorizontalArticleGenerator.Models;
using Shared;

namespace HorizontalArticleGenerator.Generators;

/// <summary>
/// Processes AI-generated article content: validates, auto-corrects, and transforms.
/// Extracted from HorizontalArticleGenerator for testability.
/// </summary>
public class ArticleContentProcessor
{
    private static readonly HashSet<string> CatchAllNamespaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "extension"
    };

    private static readonly IReadOnlyDictionary<string, string[]> UnsupportedCapabilityTermsByNamespace =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["azuremigrate"] =
            [
                "assess", "assessed", "assesses", "assessing", "assessment", "assessments",
                "replicate", "replicated", "replicates", "replicating", "replication", "replications",
                "cutover", "cut-over", "cut over"
            ]
        };

    private static readonly HashSet<string> WorkloadMigrationVerbs =
        new(["migrate", "migrates", "migrated", "migrating", "move", "moves", "moved", "moving"],
            StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> WorkloadMigrationNouns =
        new(["migration", "migrations"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> WorkloadTargetTerms =
        new(
            [
                "application", "applications", "database", "databases", "environment", "environments",
                "server", "servers", "vm", "vms", "workload", "workloads"
            ],
            StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AllowedAzureMigrateCommands =
        new(["getguidance", "request"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AzureMigrateRequestActions =
        new(["createmigrateproject", "check", "update", "generate", "download", "status"],
            StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AzureMigrateGetGuidanceOptions =
        new(["scenario", "policyname", "listpolicies", "learn"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AzureMigrateRequestOptions =
        new(
            [
                "action", "regiontype", "firewalltype", "networkarchitecture",
                "identitysubscriptionid", "managementsubscriptionid",
                "connectivitysubscriptionid", "regions", "environmentname",
                "versioncontrolsystem", "organizationname", "migrateprojectname",
                "location", "resourcegroup", "subscription", "tenant", "learn"
            ],
            StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> GetGuidancePostCommandProse =
        new(["before", "first", "again"], StringComparer.OrdinalIgnoreCase);

    private static readonly Lazy<IReadOnlyDictionary<string, string>> ConfiguredServiceDocLinks =
        new(LoadConfiguredServiceDocLinks);

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
    public ValidationResult Validate(AIGeneratedArticleData aiData, string serviceName, string? serviceIdentifier = null)
    {
        var result = new ValidationResult();

        StripTrailingPeriods(aiData, result);
        FixBrokenSentences(aiData, result);
        FixRedundantWords(aiData, result);
        ApplyConfiguredServiceDocLink(aiData, result, serviceIdentifier);
        ValidateLinkUrls(aiData, result, serviceIdentifier);
        DeduplicateAdditionalLinks(aiData, result);
        ValidateRbacRoles(aiData, result, serviceName);
        ValidateToolDescriptions(aiData, result);
        ValidateBestPracticeCount(aiData, result);
        ValidateCapabilityToolRatio(aiData, result);
        ValidateNamespaceScopeClaims(aiData, result, serviceIdentifier);
        ValidateNamespaceToolCommands(aiData, result, serviceIdentifier);

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
    public ValidationResult Process(AIGeneratedArticleData aiData, string serviceName, string? serviceIdentifier = null)
    {
        var result = Validate(aiData, serviceName, serviceIdentifier);
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
    private static void ValidateLinkUrls(AIGeneratedArticleData aiData, ValidationResult result, string? serviceIdentifier)
    {
        // Strip learn.microsoft.com prefix from serviceDocLink
        if (!string.IsNullOrEmpty(aiData.ServiceDocLink))
        {
            var cleaned = StripLearnPrefix(aiData.ServiceDocLink);
            if (cleaned != aiData.ServiceDocLink)
            {
                result.Corrections.Add("Stripped learn.microsoft.com prefix from serviceDocLink");
            }

            if (IsCatchAllNamespace(serviceIdentifier) && IsCatchAllServiceDocLink(cleaned, serviceIdentifier!))
            {
                aiData.ServiceDocLink = null;
                result.Corrections.Add($"Removed invalid serviceDocLink for catch-all namespace '{serviceIdentifier}'");
            }
            else
            {
                aiData.ServiceDocLink = cleaned;
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

    private static void ValidateNamespaceScopeClaims(
        AIGeneratedArticleData aiData,
        ValidationResult result,
        string? serviceIdentifier)
    {
        if (string.IsNullOrWhiteSpace(serviceIdentifier)
            || !UnsupportedCapabilityTermsByNamespace.TryGetValue(serviceIdentifier, out var unsupportedTerms))
        {
            return;
        }

        var generatedContent = GetGeneratedText(aiData);
        foreach (var term in unsupportedTerms)
        {
            if (Regex.IsMatch(generatedContent, $@"\b{Regex.Escape(term)}\b", RegexOptions.IgnoreCase))
            {
                result.CriticalErrors.Add(
                    $"OUT-OF-SCOPE CAPABILITY for '{serviceIdentifier}': generated content contains '{term}'.");
            }
        }

        var unsupportedWorkloadClaim = FindUnsupportedWorkloadMigrationClaim(generatedContent);
        if (unsupportedWorkloadClaim is not null)
        {
            result.CriticalErrors.Add(
                $"OUT-OF-SCOPE CAPABILITY for '{serviceIdentifier}': generated content contains '{unsupportedWorkloadClaim}'.");
        }
    }

    private static void ValidateNamespaceToolCommands(
        AIGeneratedArticleData aiData,
        ValidationResult result,
        string? serviceIdentifier)
    {
        if (!string.Equals(serviceIdentifier, "azuremigrate", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var generatedContent = GetGeneratedText(aiData);
        var actionPositions = new HashSet<int>();
        foreach (Match commandPrefixMatch in Regex.Matches(
                    generatedContent,
                    @"\bazuremigrate[^\S\r\n]+platformlandingzone\b",
                    RegexOptions.IgnoreCase))
        {
            ValidateAzureMigrateInvocation(
                generatedContent,
                commandPrefixMatch,
                actionPositions,
                result,
                serviceIdentifier!);
        }

        foreach (Match actionOptionMatch in Regex.Matches(
                    generatedContent,
                    @"(?<![a-z0-9_-])--action\b",
                    RegexOptions.IgnoreCase))
        {
            if (!actionPositions.Contains(actionOptionMatch.Index))
            {
                result.CriticalErrors.Add(
                    $"INVALID TOOL ACTION for '{serviceIdentifier}': '--action' is not part of the immediately preceding 'azuremigrate platformlandingzone request' invocation.");
            }
        }
    }

    private static void ValidateAzureMigrateInvocation(
        string generatedContent,
        Match commandPrefixMatch,
        HashSet<int> actionPositions,
        ValidationResult result,
        string serviceIdentifier)
    {
        var invocationEnd = FindInlineInvocationEnd(generatedContent, commandPrefixMatch.Index);
        var tokens = TokenizeInlineInvocation(
            generatedContent,
            commandPrefixMatch.Index + commandPrefixMatch.Length,
            invocationEnd);
        if (tokens.Count == 0 || tokens[0].Kind != InlineTokenKind.Word)
        {
            result.CriticalErrors.Add(
                $"INVENTED TOOL COMMAND for '{serviceIdentifier}': incomplete 'azuremigrate platformlandingzone' invocation.");
            return;
        }

        var commandToken = tokens[0];
        var command = commandToken.Value;
        if (!AllowedAzureMigrateCommands.Contains(command))
        {
            result.CriticalErrors.Add(
                $"INVENTED TOOL COMMAND for '{serviceIdentifier}': 'azuremigrate platformlandingzone {command}'.");
            return;
        }

        var tokenIndex = 1;
        if (tokenIndex < tokens.Count
            && tokens[tokenIndex].Kind is InlineTokenKind.PathSeparator or InlineTokenKind.Colon)
        {
            result.CriticalErrors.Add(
                $"INVENTED TOOL COMMAND for '{serviceIdentifier}': invalid path suffix follows 'azuremigrate platformlandingzone {command}'.");
            return;
        }

        if (tokenIndex < tokens.Count
            && tokens[tokenIndex].Kind is InlineTokenKind.Word or InlineTokenKind.QuotedValue)
        {
            if (string.Equals(command, "getguidance", StringComparison.OrdinalIgnoreCase)
                && tokens[tokenIndex].Kind == InlineTokenKind.Word
                && GetGuidancePostCommandProse.Contains(tokens[tokenIndex].Value))
            {
                return;
            }

            result.CriticalErrors.Add(
                $"INVALID TOOL ACTION for '{serviceIdentifier}': bare token '{tokens[tokenIndex].Value}' follows 'azuremigrate platformlandingzone {command}'; use an option.");
            return;
        }

        var knownOptions = string.Equals(command, "request", StringComparison.OrdinalIgnoreCase)
            ? AzureMigrateRequestOptions
            : AzureMigrateGetGuidanceOptions;
        var actionCount = 0;
        while (tokenIndex < tokens.Count)
        {
            var token = tokens[tokenIndex];
            if (token.Kind == InlineTokenKind.Boundary)
            {
                break;
            }

            if (token.Kind != InlineTokenKind.Option)
            {
                break;
            }

            var normalizedOption = NormalizeAzureMigrateOption(token.Value);
            if (!knownOptions.Contains(normalizedOption))
            {
                result.CriticalErrors.Add(
                    $"INVENTED TOOL COMMAND for '{serviceIdentifier}': unsupported option '{token.Value}' follows 'azuremigrate platformlandingzone {command}'.");
                return;
            }

            var isAction = string.Equals(normalizedOption, "action", StringComparison.OrdinalIgnoreCase);
            if (isAction)
            {
                actionPositions.Add(token.Start);
                actionCount++;
                if (!string.Equals(command, "request", StringComparison.OrdinalIgnoreCase))
                {
                    result.CriticalErrors.Add(
                       $"INVALID TOOL ACTION for '{serviceIdentifier}': '--action' is only valid with 'azuremigrate platformlandingzone request'.");
                }
            }

            tokenIndex++;
            if (tokenIndex < tokens.Count && tokens[tokenIndex].Kind == InlineTokenKind.Equals)
            {
                tokenIndex++;
            }

            if (tokenIndex >= tokens.Count
                || tokens[tokenIndex].Kind is not (InlineTokenKind.Word or InlineTokenKind.QuotedValue))
            {
                if (isAction)
                {
                    result.CriticalErrors.Add(
                       $"INVALID TOOL ACTION for '{serviceIdentifier}': '--action' is missing a valid value.");
                }

                continue;
            }

            var valueToken = tokens[tokenIndex++];
            if (isAction && !AzureMigrateRequestActions.Contains(valueToken.Value))
            {
                result.CriticalErrors.Add(
                    $"INVALID TOOL ACTION for '{serviceIdentifier}': unsupported '--action' value '{valueToken.Value}'.");
            }

            if (isAction && tokenIndex < tokens.Count
                && (tokens[tokenIndex].Kind == InlineTokenKind.QuotedValue
                    || tokens[tokenIndex].Kind == InlineTokenKind.Word
                    && AzureMigrateRequestActions.Contains(tokens[tokenIndex].Value)))
            {
                result.CriticalErrors.Add(
                    $"INVALID TOOL ACTION for '{serviceIdentifier}': '--action' has more than one value.");
                return;
            }

            if (tokenIndex < tokens.Count
                && tokens[tokenIndex].Kind is InlineTokenKind.Word or InlineTokenKind.QuotedValue)
            {
                break;
            }
        }

        if (actionCount > 1)
        {
            result.CriticalErrors.Add(
                $"INVALID TOOL ACTION for '{serviceIdentifier}': a request invocation can contain at most one '--action' value.");
        }
    }

    private static int FindInlineInvocationEnd(string content, int invocationStart)
    {
        var lineStart = content.LastIndexOfAny(['\r', '\n'], Math.Max(0, invocationStart - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var lineEnd = content.IndexOfAny(['\r', '\n'], invocationStart);
        lineEnd = lineEnd < 0 ? content.Length : lineEnd;

        var openingBackticks = content[lineStart..invocationStart].Count(character => character == '`');
        if (openingBackticks % 2 == 1)
        {
            var closingBacktick = content.IndexOf('`', invocationStart);
            if (closingBacktick >= 0 && closingBacktick < lineEnd)
            {
                return closingBacktick;
            }
        }

        var punctuationBoundary = content.IndexOfAny(['.', ',', ';', '!', '?', '|'], invocationStart);
        return punctuationBoundary >= 0 && punctuationBoundary < lineEnd
            ? punctuationBoundary
            : lineEnd;
    }

    private static List<InlineToken> TokenizeInlineInvocation(string content, int start, int end)
    {
        var tokens = new List<InlineToken>();
        for (var index = start; index < end;)
        {
            if (char.IsWhiteSpace(content[index]) || content[index] == '`')
            {
                index++;
                continue;
            }

            var tokenStart = index;
            if (content[index] is '"' or '\'')
            {
                var quote = content[index++];
                var valueStart = index;
                while (index < end && content[index] != quote)
                {
                   index++;
                }

                tokens.Add(new InlineToken(
                   InlineTokenKind.QuotedValue,
                   content[valueStart..index],
                   tokenStart));
                if (index < end)
                {
                   index++;
                }

                continue;
            }

            if (content[index] == '-'
                && index + 2 < end
                && content[index + 1] == '-'
                && IsInlineWordCharacter(content[index + 2]))
            {
                index += 2;
                while (index < end && IsInlineWordCharacter(content[index]))
                {
                    index++;
                }

                tokens.Add(new InlineToken(
                    InlineTokenKind.Option,
                    content[tokenStart..index],
                    tokenStart));
                continue;
            }

            if (IsInlineWordCharacter(content[index]))
            {
                index++;
                while (index < end && IsInlineWordCharacter(content[index]))
                {
                   index++;
                }

                tokens.Add(new InlineToken(
                   InlineTokenKind.Word,
                   content[tokenStart..index],
                   tokenStart));
                continue;
            }

            var kind = content[index] switch
            {
                '=' => InlineTokenKind.Equals,
                ':' => InlineTokenKind.Colon,
                '/' or '\\' => InlineTokenKind.PathSeparator,
                _ => InlineTokenKind.Boundary
            };
            tokens.Add(new InlineToken(kind, content[index].ToString(), tokenStart));
            index++;
        }

        return tokens;
    }

    private static bool IsInlineWordCharacter(char character)
        => char.IsLetterOrDigit(character) || character is '_' or '-';

    private static string NormalizeAzureMigrateOption(string option)
        => Regex.Replace(option, @"[^a-z0-9]", "", RegexOptions.IgnoreCase);

    private enum InlineTokenKind
    {
        Word,
        Option,
        QuotedValue,
        Equals,
        Colon,
        PathSeparator,
        Boundary
    }

    private readonly record struct InlineToken(InlineTokenKind Kind, string Value, int Start);

    private static string? FindUnsupportedWorkloadMigrationClaim(string generatedContent)
    {
        foreach (Match clauseMatch in Regex.Matches(
                     generatedContent,
                    @"[^\r\n.!?]+",
                    RegexOptions.IgnoreCase))
        {
            var normalizedClause = Regex.Replace(clauseMatch.Value.ToLowerInvariant(), @"[^a-z0-9]+", " ");
            var tokens = normalizedClause.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                continue;
            }

            if (FindWorkloadTarget(tokens, 0, tokens.Length) is null)
            {
                continue;
            }

            for (var index = 0; index < tokens.Length; index++)
            {
                if (WorkloadMigrationVerbs.Contains(tokens[index])
                    && !IsAzureMigrateBrand(tokens, index))
                {
                    return normalizedClause.Trim();
                }

                if (WorkloadMigrationNouns.Contains(tokens[index])
                    && !IsMigrationReadinessContext(tokens, index)
                    && HasDirectMigrationNounTarget(tokens, index))
                {
                    return normalizedClause.Trim();
                }
            }
        }

        return null;
    }

    private static bool HasDirectMigrationNounTarget(string[] tokens, int migrationIndex)
    {
        if (FindWorkloadTarget(tokens, 0, migrationIndex) is not null)
        {
            return true;
        }

        for (var index = migrationIndex + 1; index < tokens.Length; index++)
        {
            if (!WorkloadTargetTerms.Contains(tokens[index])
                && !(tokens[index] is "machine" or "machines"
                    && index > 0
                    && tokens[index - 1] == "virtual"))
            {
                continue;
            }

            return tokens[(migrationIndex + 1)..index]
                .Any(token => token is "of" or "for" or "from" or "to" or "into" or "between" or "across");
        }

        return false;
    }

    private static int? FindWorkloadTarget(string[] tokens, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (WorkloadTargetTerms.Contains(tokens[index]))
            {
                return index;
            }

            if (tokens[index] is "machine" or "machines"
                && index > start
                && tokens[index - 1] == "virtual")
            {
                return index;
            }
        }

        return null;
    }

    private static bool IsAzureMigrateBrand(string[] tokens, int index)
        => tokens[index] == "migrate"
            && index > 0
            && tokens[index - 1] == "azure";

    private static bool IsMigrationReadinessContext(string[] tokens, int index)
        => index + 1 < tokens.Length
            && tokens[index + 1] is "readiness" or "ready";

    private static string GetGeneratedText(AIGeneratedArticleData aiData)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(aiData));
        var values = new List<string>();
        CollectStringValues(document.RootElement, values);
        return string.Join('\n', values);
    }

    private static void CollectStringValues(JsonElement element, List<string> values)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                values.Add(element.GetString() ?? string.Empty);
                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectStringValues(property.Value, values);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectStringValues(item, values);
                }
                break;
        }
    }

    private static void ApplyConfiguredServiceDocLink(
        AIGeneratedArticleData aiData,
        ValidationResult result,
        string? serviceIdentifier)
    {
        if (!string.Equals(serviceIdentifier, "azuremigrate", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!ConfiguredServiceDocLinks.Value.TryGetValue("azuremigrate", out var configuredUrl)
            || string.IsNullOrWhiteSpace(configuredUrl))
        {
            result.CriticalErrors.Add(
                $"MISSING SERVICE DOC LINK for '{serviceIdentifier}' in service-doc-links.json.");
            return;
        }

        if (!string.Equals(aiData.ServiceDocLink, configuredUrl, StringComparison.OrdinalIgnoreCase))
        {
            aiData.ServiceDocLink = configuredUrl;
            result.Corrections.Add(
                $"Applied configured serviceDocLink for '{serviceIdentifier}': '{configuredUrl}'");
        }
    }

    private static IReadOnlyDictionary<string, string> LoadConfiguredServiceDocLinks()
    {
        try
        {
            var path = Path.Combine(DataFileLoader.GetDataDirectoryPath(), "service-doc-links.json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.EnumerateObject()
                .Where(property => property.Value.TryGetProperty("url", out var url)
                    && !string.IsNullOrWhiteSpace(url.GetString()))
                .ToDictionary(
                    property => property.Name,
                    property => property.Value.GetProperty("url").GetString()!,
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

    private static bool IsCatchAllNamespace(string? serviceIdentifier)
        => !string.IsNullOrWhiteSpace(serviceIdentifier) && CatchAllNamespaces.Contains(serviceIdentifier);

    private static bool IsCatchAllServiceDocLink(string url, string serviceIdentifier)
    {
        var normalized = url.TrimEnd('/');
        var namespacePath = $"/azure/{serviceIdentifier}";
        return normalized.Equals(namespacePath, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith($"{namespacePath}/", StringComparison.OrdinalIgnoreCase);
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
