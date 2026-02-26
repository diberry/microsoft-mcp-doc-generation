// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using SkillsRelevance.Models;

namespace SkillsRelevance.Services;

/// <summary>
/// Analyzes skills for relevance to a given Azure service or MCP namespace.
/// Uses keyword matching to score relevance (0.0 = not relevant, 1.0 = highly relevant).
/// </summary>
public class SkillRelevanceAnalyzer
{
    private readonly string _serviceNameInput;
    private readonly List<string> _searchTerms;

    public SkillRelevanceAnalyzer(string serviceNameOrNamespace)
    {
        _serviceNameInput = serviceNameOrNamespace;
        _searchTerms = BuildSearchTerms(serviceNameOrNamespace);
    }

    /// <summary>
    /// Scores the skill for relevance. Returns score between 0.0 and 1.0.
    /// Also populates skill.RelevanceReasons.
    /// </summary>
    public double Score(SkillInfo skill)
    {
        var reasons = new List<string>();
        double score = 0.0;

        // Check skill name (high weight)
        foreach (var term in _searchTerms)
        {
            if (skill.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.4;
                reasons.Add($"Name contains '{term}'");
                break;
            }
        }

        // Check filename (high weight)
        foreach (var term in _searchTerms)
        {
            if (skill.FileName.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                if (score < 0.4) score += 0.3;
                reasons.Add($"File name contains '{term}'");
                break;
            }
        }

        // Check description (medium weight)
        foreach (var term in _searchTerms)
        {
            if (skill.Description.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.2;
                reasons.Add($"Description mentions '{term}'");
                break;
            }
        }

        // Check tags (medium weight)
        foreach (var tag in skill.Tags)
        {
            foreach (var term in _searchTerms)
            {
                if (tag.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.2;
                    reasons.Add($"Tag '{tag}' matches '{term}'");
                    break;
                }
            }
        }

        // Check Azure services list (medium weight)
        foreach (var svc in skill.AzureServices)
        {
            foreach (var term in _searchTerms)
            {
                if (svc.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    score += 0.2;
                    reasons.Add($"Azure service '{svc}' matches '{term}'");
                    break;
                }
            }
        }

        // Check raw content (lower weight - broad match)
        var contentMatches = 0;
        foreach (var term in _searchTerms)
        {
            var count = CountOccurrences(skill.RawContent, term);
            if (count > 0)
            {
                contentMatches += count;
            }
        }
        if (contentMatches > 0)
        {
            var contentScore = Math.Min(0.3, contentMatches * 0.05);
            score += contentScore;
            reasons.Add($"Content mentions terms {contentMatches} time(s)");
        }

        // Cap score at 1.0
        score = Math.Min(1.0, score);
        skill.RelevanceScore = score;
        skill.RelevanceReasons = reasons;
        return score;
    }

    /// <summary>
    /// Filters and sorts skills by relevance score.
    /// </summary>
    public List<SkillInfo> FilterAndSort(IEnumerable<SkillInfo> skills, double minScore = 0.1)
    {
        return skills
            .Where(s => Score(s) >= minScore)
            .OrderByDescending(s => s.RelevanceScore)
            .ToList();
    }

    /// <summary>
    /// Builds a list of search terms from the service name/namespace input.
    /// Handles variations like "aks" → ["aks", "kubernetes", "container"]
    /// </summary>
    internal static List<string> BuildSearchTerms(string input)
    {
        var terms = new List<string>();

        // Add the raw input
        terms.Add(input.Trim());

        // Split on hyphens, underscores, spaces
        var parts = input.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Length > 2 && !terms.Contains(part, StringComparer.OrdinalIgnoreCase))
                terms.Add(part);
        }

        // Common namespace abbreviation expansions
        var expansions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["aks"] = ["kubernetes", "k8s", "container orchestration"],
            ["acr"] = ["container registry", "docker", "container image"],
            ["appservice"] = ["app service", "web app", "webapp"],
            ["kv"] = ["key vault", "secret", "certificate"],
            ["keyvault"] = ["key vault", "secret", "certificate"],
            ["storage"] = ["blob", "azure storage", "file share"],
            ["cosmosdb"] = ["cosmos", "nosql", "document database"],
            ["sql"] = ["azure sql", "database"],
            ["servicebus"] = ["service bus", "messaging", "queue"],
            ["eventhub"] = ["event hub", "event streaming"],
            ["monitor"] = ["azure monitor", "log analytics", "metrics"],
            ["openai"] = ["azure openai", "gpt", "cognitive services"],
            ["ai"] = ["azure ai", "cognitive services", "machine learning"],
            ["devops"] = ["azure devops", "pipelines", "boards", "repos"],
            ["network"] = ["virtual network", "vnet", "nsg", "load balancer"],
            ["vm"] = ["virtual machine", "compute"],
            ["functions"] = ["azure functions", "serverless", "function app"],
            ["apim"] = ["api management", "api gateway"],
            ["logicapps"] = ["logic apps", "workflow"],
            ["datafactory"] = ["data factory", "adf", "data integration"],
        };

        if (expansions.TryGetValue(input.Trim(), out var expanded))
        {
            foreach (var exp in expanded)
            {
                if (!terms.Contains(exp, StringComparer.OrdinalIgnoreCase))
                    terms.Add(exp);
            }
        }

        return terms.Where(t => !string.IsNullOrEmpty(t)).ToList();
    }

    private static int CountOccurrences(string text, string term)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
            return 0;

        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += term.Length;
        }
        return count;
    }
}
