// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using NUnit.Framework;
using SkillsRelevance.Models;
using SkillsRelevance.Services;

namespace SkillsRelevance.Tests;

[TestFixture]
public class SkillRelevanceAnalyzerTests
{
    // ── BuildSearchTerms ────────────────────────────────────────────────

    [Test]
    public void BuildSearchTerms_ReturnsRawInput()
    {
        var terms = SkillRelevanceAnalyzer.BuildSearchTerms("storage");
        Assert.That(terms, Contains.Item("storage"));
    }

    [Test]
    public void BuildSearchTerms_AksExpandsToKubernetesAndK8s()
    {
        var terms = SkillRelevanceAnalyzer.BuildSearchTerms("aks");
        Assert.That(terms, Contains.Item("kubernetes"));
        Assert.That(terms, Contains.Item("k8s"));
    }

    [Test]
    public void BuildSearchTerms_KeyvaultExpandsToSecretAndCertificate()
    {
        var terms = SkillRelevanceAnalyzer.BuildSearchTerms("keyvault");
        Assert.That(terms, Contains.Item("key vault"));
        Assert.That(terms, Contains.Item("secret"));
        Assert.That(terms, Contains.Item("certificate"));
    }

    [Test]
    public void BuildSearchTerms_SplitsHyphenatedInput()
    {
        var terms = SkillRelevanceAnalyzer.BuildSearchTerms("azure-storage");
        Assert.That(terms, Contains.Item("azure"));
        Assert.That(terms, Contains.Item("storage"));
    }

    [Test]
    public void BuildSearchTerms_FiltersShortParts()
    {
        // Parts of length ≤ 2 should not be added as separate terms
        var terms = SkillRelevanceAnalyzer.BuildSearchTerms("vm");
        // "vm" is the raw input so it stays; no additional split items since it's only one part
        Assert.That(terms, Contains.Item("vm"));
    }

    [Test]
    public void BuildSearchTerms_NoDuplicates()
    {
        var terms = SkillRelevanceAnalyzer.BuildSearchTerms("storage");
        // "storage" should appear exactly once
        Assert.That(terms.Count(t => string.Equals(t, "storage", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(1));
    }

    // ── Score ────────────────────────────────────────────────────────────

    [Test]
    public void Score_NameMatch_ReturnsHighScore()
    {
        var analyzer = new SkillRelevanceAnalyzer("keyvault");
        var skill = new SkillInfo { Name = "Azure Key Vault Best Practices", FileName = "keyvault-tips.md" };
        var score = analyzer.Score(skill);
        Assert.That(score, Is.GreaterThanOrEqualTo(0.4));
    }

    [Test]
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
        Assert.That(score, Is.EqualTo(0.0));
    }

    [Test]
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
        Assert.That(score, Is.GreaterThan(0.0));
        Assert.That(skill.RelevanceReasons, Has.Some.Contains("Description mentions"));
    }

    [Test]
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
        Assert.That(score, Is.GreaterThan(0.0));
        Assert.That(skill.RelevanceReasons, Has.Some.Contains("Tag"));
    }

    [Test]
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
        Assert.That(score, Is.GreaterThan(0.0));
    }

    [Test]
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
        Assert.That(score, Is.LessThanOrEqualTo(1.0));
    }

    [Test]
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
        Assert.That(skill.RelevanceReasons, Is.Not.Empty);
    }

    // ── FilterAndSort ────────────────────────────────────────────────────

    [Test]
    public void FilterAndSort_ExcludesBelowMinScore()
    {
        var analyzer = new SkillRelevanceAnalyzer("cosmosdb");
        var skills = new List<SkillInfo>
        {
            new() { Name = "CosmosDB Guide", FileName = "cosmosdb.md", RawContent = "cosmosdb nosql" },
            new() { Name = "Unrelated Skill", FileName = "unrelated.md", Description = "Nothing relevant here." }
        };

        var results = analyzer.FilterAndSort(skills, minScore: 0.1);
        Assert.That(results, Has.All.Matches<SkillInfo>(s => s.RelevanceScore >= 0.1));
    }

    [Test]
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
        Assert.That(results.Count, Is.GreaterThan(0));
        for (int i = 1; i < results.Count; i++)
        {
            Assert.That(results[i - 1].RelevanceScore, Is.GreaterThanOrEqualTo(results[i].RelevanceScore));
        }
    }

    [Test]
    public void FilterAndSort_AllSkillsMode_ReturnsAllWhenMinScoreIsZero()
    {
        var analyzer = new SkillRelevanceAnalyzer("openai");
        var skills = new List<SkillInfo>
        {
            new() { Name = "Azure OpenAI Skill", FileName = "openai.md" },
            new() { Name = "Git Workflow", FileName = "git.md" }
        };

        var results = analyzer.FilterAndSort(skills, minScore: 0.0);
        Assert.That(results.Count, Is.EqualTo(2));
    }
}
