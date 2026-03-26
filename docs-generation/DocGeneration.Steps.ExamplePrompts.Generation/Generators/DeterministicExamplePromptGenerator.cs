// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using ExamplePromptGeneratorStandalone.Models;

namespace ExamplePromptGeneratorStandalone.Generators;

/// <summary>
/// Generates example prompts deterministically using verb-based templates
/// instead of AI calls. Covers ~50% of tools (list, get, create, delete, update).
/// Falls back to AI for complex or unusual operations.
/// Fixes: #163 Tier 2a
/// </summary>
public static class DeterministicExamplePromptGenerator
{
    private static readonly HashSet<string> StandardVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "list", "get", "create", "delete", "update"
    };

    private static readonly Dictionary<string, string[]> ValueBank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["account"] = ["mystorageacct", "prodstore2026", "companydata2024", "webappstorage", "mediaacct2024"],
        ["vault"] = ["prod-kv", "dev-keyvault", "finance-kv", "webapp-kv", "backup-kv"],
        ["vaultname"] = ["prod-kv", "dev-keyvault", "finance-kv", "webapp-kv", "backup-kv"],
        ["resource-group"] = ["rg-prod", "my-resource-group", "rg-dev", "rg-company", "rg-analytics"],
        ["subscription"] = ["my-subscription", "contoso-sub", "dev-subscription", "prod-sub", "test-sub"],
        ["location"] = ["eastus", "westus2", "centralus", "northcentralus", "eastus2"],
        ["server-name"] = ["prod-sql-server", "dev-pg-server", "test-server-01", "analytics-server", "backup-server"],
        ["servername"] = ["prod-sql-server", "dev-pg-server", "test-server-01", "analytics-server", "backup-server"],
        ["database-name"] = ["mydb", "prod-database", "analytics-db", "app-data", "user-store"],
        ["databasename"] = ["mydb", "prod-database", "analytics-db", "app-data", "user-store"],
        ["container-name"] = ["backups", "documents", "images", "logs", "media"],
        ["containername"] = ["backups", "documents", "images", "logs", "media"],
        ["name"] = ["my-resource", "prod-item-01", "dev-config", "test-resource-2026", "analytics-asset"],
        ["secret-name"] = ["db-password", "api-key", "oauth-token", "storage-conn-string", "payment-key"],
        ["secretname"] = ["db-password", "api-key", "oauth-token", "storage-conn-string", "payment-key"],
        ["key-name"] = ["signing-key", "encryption-key", "rsa-key-01", "auth-key", "backup-key"],
        ["keyname"] = ["signing-key", "encryption-key", "rsa-key-01", "auth-key", "backup-key"],
        ["value"] = ["P@ssw0rd!2026", "sk_live_4f3b2a", "DefaultEndpointsProtocol=https", "eyJhbGciOi", "pg_live_98zxy"],
        ["query"] = ["Heartbeat | take 10", "AzureMetrics | summarize count()", "requests | where success == false", "traces | top 5 by timestamp", "exceptions | count"],
        ["planid"] = ["plan-001", "marketing-plan", "dev-sprint-q1", "onboarding-plan", "project-alpha"],
        ["taskid"] = ["task-001", "review-docs", "fix-bug-42", "deploy-staging", "update-config"],
        ["groupid"] = ["group-engineering", "team-marketing", "dept-finance", "org-contoso", "project-alpha"],
        ["indexname"] = ["products-index", "search-docs", "knowledge-base", "catalog-idx", "content-index"],
    };

    private static readonly string[] DefaultValues = ["my-value-1", "prod-value-02", "test-config-a", "dev-item-2026", "sample-value"];

    /// <summary>
    /// Classifies the verb from a tool command string.
    /// Returns null if the verb is not a standard verb.
    /// </summary>
    public static string? ClassifyVerb(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var segments = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return null;
        var lastSegment = segments[^1].ToLowerInvariant();
        return StandardVerbs.Contains(lastSegment) ? lastSegment : null;
    }

    private static readonly HashSet<string> CommonParamNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "retry-delay", "retry-max-delay", "retry-max-retries", "retry-mode",
        "retry-network-timeout", "auth-method", "tenant", "subscription", "resource-group"
    };

    /// <summary>
    /// Returns true if the parameter is a common infrastructure parameter
    /// that does not change a tool's mode of operation.
    /// </summary>
    public static bool IsCommonParam(string paramName)
    {
        return CommonParamNames.Contains(paramName);
    }

    /// <summary>
    /// Checks if a tool is eligible for deterministic prompt generation.
    /// Returns false for non-standard verbs, when e2e prompts exist,
    /// or when the tool has non-common optional parameters (dual-mode tools).
    /// </summary>
    public static bool IsEligible(Tool tool, bool hasE2ePrompts)
    {
        if (hasE2ePrompts) return false;
        if (ClassifyVerb(tool.Command) == null) return false;

        // Dual-mode tools with non-common optional params should use AI generation
        var nonCommonOptionalParams = tool.Option?
            .Where(o => !o.Required && !IsCommonParam(o.Name ?? ""))
            .ToList() ?? new List<Option>();

        if (nonCommonOptionalParams.Count > 0) return false;

        return true;
    }

    /// <summary>
    /// Extracts the resource type from a command string.
    /// For "storage account list" returns "account".
    /// For "redis list" returns "redis".
    /// </summary>
    public static string ExtractResource(string command)
    {
        var segments = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 2) return segments[0];
        // Middle segments (skip namespace and verb)
        return string.Join(" ", segments.Skip(1).Take(segments.Length - 2));
    }

    /// <summary>
    /// Generates an ExamplePromptsResponse deterministically for a tool.
    /// </summary>
    public static ExamplePromptsResponse Generate(Tool tool)
    {
        var verb = ClassifyVerb(tool.Command!)!;
        var resource = ExtractResource(tool.Command!);
        var requiredParams = tool.Option?
            .Where(o => o.Required)
            .ToList() ?? new List<Option>();

        var prompts = GeneratePrompts(verb, resource, requiredParams);

        return new ExamplePromptsResponse
        {
            ToolName = tool.Name,
            Prompts = prompts
        };
    }

    private static List<string> GeneratePrompts(string verb, string resource, List<Option> requiredParams)
    {
        var templates = GetTemplates(verb, resource);
        var prompts = new List<string>();

        for (int i = 0; i < 5; i++)
        {
            var paramPhrase = BuildParamPhrase(requiredParams, i);
            var prompt = templates[i];

            if (!string.IsNullOrEmpty(paramPhrase))
            {
                prompt = prompt.Replace("{params}", paramPhrase);
            }
            else
            {
                // No required params — remove placeholder
                prompt = prompt.Replace(" in {params}", "").Replace(" with {params}", "")
                    .Replace(" from {params}", "").Replace(" for {params}", "");
            }

            prompts.Add(prompt);
        }

        return prompts;
    }

    private static string[] GetTemplates(string verb, string resource)
    {
        var r = FormatResource(resource);
        var rPlural = Pluralize(r);

        return verb.ToLowerInvariant() switch
        {
            "list" =>
            [
                $"List all {rPlural} in {{params}}.",
                $"Show me the {rPlural} in {{params}}.",
                $"What {rPlural} exist in {{params}}?",
                $"Get all {rPlural} from {{params}}.",
                $"Display {rPlural} in {{params}}.",
            ],
            "get" =>
            [
                $"Get {r} details from {{params}}.",
                $"Show me the {r} in {{params}}.",
                $"Retrieve {r} information from {{params}}.",
                $"Display the {r} in {{params}}.",
                $"What are the {r} details in {{params}}?",
            ],
            "create" =>
            [
                $"Create a new {r} in {{params}}.",
                $"Set up a {r} in {{params}}.",
                $"Add a {r} in {{params}}.",
                $"Create {r} in {{params}}.",
                $"Can you create a {r} in {{params}}?",
            ],
            "delete" =>
            [
                $"Delete the {r} in {{params}}.",
                $"Remove the {r} from {{params}}.",
                $"Delete {r} in {{params}}.",
                $"Can you remove the {r} from {{params}}?",
                $"Remove {r} in {{params}}.",
            ],
            "update" =>
            [
                $"Update the {r} in {{params}}.",
                $"Modify the {r} in {{params}}.",
                $"Change the {r} settings in {{params}}.",
                $"Update {r} in {{params}}.",
                $"Can you update the {r} in {{params}}?",
            ],
            _ => throw new ArgumentException($"Unsupported verb: {verb}")
        };
    }

    private static string BuildParamPhrase(List<Option> requiredParams, int valueIndex)
    {
        if (requiredParams.Count == 0) return string.Empty;

        var phrases = requiredParams.Select(p =>
        {
            var displayName = FormatParamDisplayName(p.Name!);
            var value = GetValue(p.Name!, valueIndex);
            return $"{displayName} '{value}'";
        });

        return string.Join(" and ", phrases);
    }

    private static string FormatParamDisplayName(string paramName)
    {
        // Map common parameter names to natural display names
        var normalized = paramName.ToLowerInvariant().Replace("-", "");
        return normalized switch
        {
            "account" => "storage account",
            "vault" or "vaultname" => "key vault",
            "resourcegroup" => "resource group",
            "subscription" => "subscription",
            "location" => "location",
            "servername" => "server",
            "databasename" => "database",
            "containername" => "container",
            "secretname" => "secret",
            "keyname" => "key",
            "indexname" => "index",
            "planid" => "plan",
            "taskid" => "task",
            "groupid" => "group",
            _ => paramName.Replace("-", " ")
        };
    }

    private static string GetValue(string paramName, int index)
    {
        var normalized = paramName.ToLowerInvariant().Replace("-", "");
        // Try exact match first, then normalized
        if (ValueBank.TryGetValue(paramName, out var values))
            return values[index % values.Length];
        if (ValueBank.TryGetValue(normalized, out values))
            return values[index % values.Length];
        return DefaultValues[index % DefaultValues.Length];
    }

    private static string FormatResource(string resource)
    {
        // Clean up resource name for natural language
        return resource.Replace("-", " ").Replace("_", " ").ToLowerInvariant();
    }

    private static string Pluralize(string resource)
    {
        if (string.IsNullOrEmpty(resource)) return resource;
        if (resource.EndsWith("s") || resource.EndsWith("x") || resource.EndsWith("sh") || resource.EndsWith("ch"))
            return resource + "es";
        if (resource.EndsWith("y") && !resource.EndsWith("ay") && !resource.EndsWith("ey") && !resource.EndsWith("oy") && !resource.EndsWith("uy"))
            return resource[..^1] + "ies";
        return resource + "s";
    }
}