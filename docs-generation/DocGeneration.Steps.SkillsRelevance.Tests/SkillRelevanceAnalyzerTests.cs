// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using SkillsRelevance.Models;
using SkillsRelevance.Services;

namespace SkillsRelevance.Tests;

public class SkillRelevanceAnalyzerTests
{
    // ── BuildSearchTerms ────────────────────────────────────────────────

    [Fact]
    public void BuildSearchTerms_ReturnsRawInput()
    {
        var terms = SkillRelevanceAnalyzer.BuildSearchTerms("storage");
        Assert.Contains("storage", terms);
    }

    [Fact]
    public void BuildSearchTerms_AksExpandsToKubernetesAndK8s()
    {
        var terms = SkillRelevanceAnalyzer.BuildSearchTerms("aks");
        Assert.Contains("kubernetes", terms);
        Assert.Contains("k8s", terms);
    }

    [Fact]
    public void BuildSearchTerms_KeyvaultExpandsToSecretAndCertificate()
    {
        var terms = SkillRelevanceAnalyzer.BuildSearchTerms("keyvault");
        Assert.Contains("key vault", terms);
        Assert.Contains("secret", terms);
        Assert.Contains("certificate", terms);
    }

    [Fact]
    public void BuildSearchTerms_SplitsHyphenatedInput()
    {
        var terms = SkillRelevanceAnalyzer.BuildSearchTerms("azure-storage");
        Assert.Contains("azure", terms);
        Assert.Contains("storage", terms);
    }

    [Fact]
    public void BuildSearchTerms_FiltersShortParts()
    {
        // Parts of length ≤ 2 should not be added as separate terms
        var terms = SkillRelevanceAnalyzer.BuildSearchTerms("vm");
        // "vm" is the raw input so it stays; no additional split items since it's only one part
        Assert.Contains("vm", terms);
    }

    [Fact]
    public void BuildSearchTerms_NoDuplicates()
    {
        var terms = SkillRelevanceAnalyzer.BuildSearchTerms("storage");
        // "storage" should appear exactly once
        Assert.Equal(1, terms.Count(t => string.Equals(t, "storage", StringComparison.OrdinalIgnoreCase)));
    }

    // ── Score ────────────────────────────────────────────────────────────

    [Fact]
    public void Score_NameMatch_ReturnsHighScore()
    {
        var analyzer = new SkillRelevanceAnalyzer("keyvault");
        var skill = new SkillInfo { Name = "Azure Key Vault Best Practices", FileName = "keyvault-tips.md" };
        var score = analyzer.Score(skill);
        Assert.True(score >= 0.4);
    }

    [Fact]
    public void Score_NoMatch_ReturnsZero()
    {
        var analyzer = new SkillRelevanceAnalyzer("cosmosdb");
        var skill = new SkillInfo
        {
            Name = "GitHub Pull Request Workflow",
            FileName = "github-pr.md",
            Description = "How to review pull requests effectively.",
            RawContent = "Review pull requests using GitHub's interface.",
            Tags = new List<string> { "github", "pr" }
        };
        var score = analyzer.Score(skill);
        Assert.Equal(0.0, score);
    }

    [Fact]
    public void Score_DescriptionMatch_AddsScore()
    {
        var analyzer = new SkillRelevanceAnalyzer("storage");
        var skill = new SkillInfo
        {
            Name = "Cloud Utilities",
            FileName = "cloud.md",
            Description = "Use Azure Storage blobs for large file uploads."
        };
        var score = analyzer.Score(skill);
        Assert.True(score > 0.0);
        Assert.Contains(skill.RelevanceReasons, r => r.Contains("Description mentions"));
    }

    [Fact]
    public void Score_TagMatch_AddsScore()
    {
        var analyzer = new SkillRelevanceAnalyzer("sql");
        var skill = new SkillInfo
        {
            Name = "Database Helper",
            FileName = "db.md",
            Tags = new List<string> { "azure sql", "relational" }
        };
        var score = analyzer.Score(skill);
        Assert.True(score > 0.0);
        Assert.Contains(skill.RelevanceReasons, r => r.Contains("Tag"));
    }

    [Fact]
    public void Score_ContentMatches_AddsScore()
    {
        var analyzer = new SkillRelevanceAnalyzer("monitor");
        var skill = new SkillInfo
        {
            Name = "Observability Guide",
            FileName = "observability.md",
            RawContent = "Use Azure Monitor to collect metrics. Azure Monitor alerts help you."
        };
        var score = analyzer.Score(skill);
        Assert.True(score > 0.0);
    }

    [Fact]
    public void Score_CapsAt1()
    {
        var analyzer = new SkillRelevanceAnalyzer("aks");
        // Name, file, description, tags, services, and lots of content matches
        var skill = new SkillInfo
        {
            Name = "AKS Kubernetes Best Practices",
            FileName = "aks-kubernetes-guide.md",
            Description = "AKS kubernetes cluster management",
            Tags = new List<string> { "aks", "kubernetes" },
            AzureServices = new List<string> { "Azure Kubernetes Service" },
            RawContent = string.Concat(Enumerable.Repeat("aks kubernetes k8s ", 20))
        };
        var score = analyzer.Score(skill);
        Assert.True(score <= 1.0);
    }

    [Fact]
    public void Score_PopulatesRelevanceReasons()
    {
        var analyzer = new SkillRelevanceAnalyzer("storage");
        var skill = new SkillInfo
        {
            Name = "Azure Storage Guide",
            FileName = "storage.md",
            RawContent = "storage"
        };
        analyzer.Score(skill);
        Assert.NotEmpty(skill.RelevanceReasons);
    }

    // ── FilterAndSort ────────────────────────────────────────────────────

    [Fact]
    public void FilterAndSort_ExcludesBelowMinScore()
    {
        var analyzer = new SkillRelevanceAnalyzer("cosmosdb");
        var skills = new List<SkillInfo>
        {
            new() { Name = "CosmosDB Guide", FileName = "cosmosdb.md", RawContent = "cosmosdb nosql" },
            new() { Name = "Unrelated Skill", FileName = "unrelated.md", Description = "Nothing relevant here." }
        };

        var results = analyzer.FilterAndSort(skills, minScore: 0.1);
        Assert.All(results, s => Assert.True(s.RelevanceScore >= 0.1));
    }

    [Fact]
    public void FilterAndSort_OrdersByScoreDescending()
    {
        var analyzer = new SkillRelevanceAnalyzer("sql");
        var skills = new List<SkillInfo>
        {
            new() { Name = "SQL Tip", FileName = "sql.md", RawContent = "sql" },
            new() { Name = "Azure SQL Expert Guide", FileName = "azure-sql-expert.md",
                    Description = "Comprehensive Azure SQL guide", RawContent = "azure sql database queries" }
        };

        var results = analyzer.FilterAndSort(skills, minScore: 0.0);
        Assert.True(results.Count > 0);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].RelevanceScore >= results[i].RelevanceScore);
        }
    }

    [Fact]
    public void FilterAndSort_AllSkillsMode_ReturnsAllWhenMinScoreIsZero()
    {
        var analyzer = new SkillRelevanceAnalyzer("openai");
        var skills = new List<SkillInfo>
        {
            new() { Name = "Azure OpenAI Skill", FileName = "openai.md" },
            new() { Name = "Git Workflow", FileName = "git.md" }
        };

        var results = analyzer.FilterAndSort(skills, minScore: 0.0);
        Assert.Equal(2, results.Count);
    }
}
