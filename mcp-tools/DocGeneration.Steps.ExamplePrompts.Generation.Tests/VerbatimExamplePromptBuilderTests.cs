// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ExamplePromptGeneratorStandalone.Generators;
using ExamplePromptGeneratorStandalone.Models;
using Xunit;

namespace ExamplePromptGeneratorStandalone.Tests;

/// <summary>
/// Tests for the verbatim example prompt path (issue #748).
/// Tools that have source prompts in e2eTestPrompts.md must be published
/// VERBATIM — exact text, exact count, exact order, and NO AI call.
/// These tests would FAIL if the verbatim path rewrote, capped, reordered,
/// or truncated the source prompts.
/// Uses varied Azure services per the Universal Design Principle.
/// </summary>
public class VerbatimExamplePromptBuilderTests
{
    // ── Exact text preservation ─────────────────────────────────────

    [Fact]
    public void Build_PreservesExactPromptText_NoRewrite()
    {
        var tool = new Tool { Command = "storage account list", Name = "storage-account-list" };
        var source = new List<string>
        {
            "List all storage accounts in my subscription",
            "Show me the storage accounts in resource group rg-prod"
        };

        var result = VerbatimExamplePromptBuilder.Build(tool, source);

        Assert.Equal(source[0], result.Prompts[0]);
        Assert.Equal(source[1], result.Prompts[1]);
    }

    [Fact]
    public void Build_SetsToolNameFromCommand()
    {
        var tool = new Tool { Command = "keyvault secret get", Name = "keyvault-secret-get" };
        var source = new List<string> { "Get the secret named db-password from prod-kv" };

        var result = VerbatimExamplePromptBuilder.Build(tool, source);

        Assert.Equal("keyvault secret get", result.ToolName);
    }

    // ── Count preservation (NOT capped to 5 or 10) ──────────────────

    [Fact]
    public void Build_PreservesSingleSourcePrompt()
    {
        var tool = new Tool { Command = "cosmos database list", Name = "cosmos-database-list" };
        var source = new List<string> { "List Cosmos DB databases in account mycosmos" };

        var result = VerbatimExamplePromptBuilder.Build(tool, source);

        Assert.Single(result.Prompts);
    }

    [Fact]
    public void Build_PreservesNineteenSourcePrompts_NotCapped()
    {
        var tool = new Tool { Command = "monitor logs query", Name = "monitor-logs-query" };
        var source = new List<string>();
        for (int i = 1; i <= 19; i++)
        {
            source.Add($"Query Azure Monitor logs variant {i}");
        }

        var result = VerbatimExamplePromptBuilder.Build(tool, source);

        Assert.Equal(19, result.Prompts.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(11)]
    [InlineData(19)]
    public void Build_PreservesArbitraryCount(int count)
    {
        var tool = new Tool { Command = "aks cluster get", Name = "aks-cluster-get" };
        var source = new List<string>();
        for (int i = 0; i < count; i++)
        {
            source.Add($"Get AKS cluster prompt {i}");
        }

        var result = VerbatimExamplePromptBuilder.Build(tool, source);

        Assert.Equal(count, result.Prompts.Count);
    }

    // ── Order preservation ──────────────────────────────────────────

    [Fact]
    public void Build_PreservesSourceOrder()
    {
        var tool = new Tool { Command = "monitor metrics query", Name = "monitor-metrics-query" };
        var source = new List<string>
        {
            "First: show CPU metrics",
            "Second: show memory metrics",
            "Third: show disk metrics",
            "Fourth: show network metrics"
        };

        var result = VerbatimExamplePromptBuilder.Build(tool, source);

        Assert.Equal(source, result.Prompts);
    }

    // ── No AI call / no mutation of underlying list ─────────────────

    [Fact]
    public void Build_DoesNotMutateSourceList()
    {
        var tool = new Tool { Command = "aks cluster list", Name = "aks-cluster-list" };
        var source = new List<string> { "List AKS clusters", "Show my Kubernetes clusters" };

        var result = VerbatimExamplePromptBuilder.Build(tool, source);
        result.Prompts.Add("mutated");

        Assert.Equal(2, source.Count);
    }

    [Fact]
    public void Build_ProvenanceNote_ReportsSourceCountAndNoAi()
    {
        var tool = new Tool { Command = "storage blob upload", Name = "storage-blob-upload" };
        var source = new List<string> { "Upload a file to container backups", "Upload media.png to images" };

        var note = VerbatimExamplePromptBuilder.BuildProvenanceNote(source);

        Assert.Contains("verbatim", note, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", note);
        Assert.Contains("e2eTestPrompts.md", note);
        Assert.Contains("no AI call", note, StringComparison.OrdinalIgnoreCase);
    }
}
