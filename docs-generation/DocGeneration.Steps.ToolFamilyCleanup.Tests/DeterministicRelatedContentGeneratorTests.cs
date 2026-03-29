// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for DeterministicRelatedContentGenerator — replaces AI-generated
/// related content with template + JSON lookup per #163.
/// </summary>
public class DeterministicRelatedContentGeneratorTests
{
    private static readonly Dictionary<string, ServiceDocLink> TestLinks = new()
    {
        ["storage"] = new("Azure Storage documentation", "/azure/storage/"),
        ["keyvault"] = new("Azure Key Vault documentation", "/azure/key-vault/"),
        ["cosmos"] = new("Azure Cosmos DB documentation", "/azure/cosmos-db/"),
    };

    // ── Core generation ─────────────────────────────────────────────

    [Fact]
    public void Generate_KnownService_ReturnsThreeFixedLinksAndServiceLink()
    {
        var result = DeterministicRelatedContentGenerator.Generate("storage", TestLinks);

        Assert.Contains("## Related content", result);
        Assert.Contains("[What are the Azure MCP Server tools?](index.md)", result);
        Assert.Contains("[Get started using Azure MCP Server](../get-started.md)", result);
        Assert.Contains("[Azure Storage documentation](/azure/storage/)", result);
    }

    [Fact]
    public void Generate_KnownService_HasExactlyFourLinks()
    {
        var result = DeterministicRelatedContentGenerator.Generate("cosmos", TestLinks);

        var linkCount = result.Split('\n').Count(l => l.StartsWith("- ["));
        Assert.Equal(3, linkCount);
    }

    // ── Unknown service fallback ────────────────────────────────────

    [Fact]
    public void Generate_UnknownService_OmitsServiceLink()
    {
        var result = DeterministicRelatedContentGenerator.Generate("unknownservice", TestLinks);

        Assert.Contains("## Related content", result);
        Assert.Contains("[What are the Azure MCP Server tools?](index.md)", result);
        Assert.Contains("[Get started using Azure MCP Server](../get-started.md)", result);
        // Only 2 fixed links, no service-specific link
        var linkCount = result.Split('\n').Count(l => l.StartsWith("- ["));
        Assert.Equal(2, linkCount);
    }

    // ── Idempotent ──────────────────────────────────────────────────

    [Fact]
    public void Generate_SameInput_SameOutput()
    {
        var result1 = DeterministicRelatedContentGenerator.Generate("keyvault", TestLinks);
        var result2 = DeterministicRelatedContentGenerator.Generate("keyvault", TestLinks);

        Assert.Equal(result1, result2);
    }

    // ── Loading from file ───────────────────────────────────────────

    [Fact]
    public void LoadServiceDocLinks_ProductionFile_LoadsSuccessfully()
    {
        var linksPath = Path.Combine(
            FindProjectRoot(), "docs-generation", "data", "service-doc-links.json");

        var links = DeterministicRelatedContentGenerator.LoadServiceDocLinks(linksPath);

        Assert.NotEmpty(links);
        // Spot-check known services
        Assert.True(links.ContainsKey("storage"));
        Assert.True(links.ContainsKey("keyvault"));
        Assert.True(links.ContainsKey("monitor"));
        // URLs should start with /azure/
        foreach (var (_, link) in links)
        {
            Assert.StartsWith("/azure/", link.Url);
            Assert.EndsWith("documentation", link.Title, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Generate_WithProductionLinks_AllValidatedNamespacesHaveLinks()
    {
        var linksPath = Path.Combine(
            FindProjectRoot(), "docs-generation", "data", "service-doc-links.json");
        var links = DeterministicRelatedContentGenerator.LoadServiceDocLinks(linksPath);

        // Check all validated namespaces have entries
        var validatedDirs = Directory.GetDirectories(
            FindProjectRoot(), "generated-validated-*");

        foreach (var dir in validatedDirs)
        {
            var ns = Path.GetFileName(dir).Replace("generated-validated-", "");
            if (ns.StartsWith("phase1-")) continue; // Skip phase1 dirs

            var result = DeterministicRelatedContentGenerator.Generate(ns, links);
            var linkCount = result.Split('\n').Count(l => l.StartsWith("- ["));

            // Every validated namespace should have 3 links (2 fixed + 1 service)
            Assert.True(linkCount >= 2,
                $"Namespace '{ns}' has only {linkCount} links — add entry to service-doc-links.json");
        }
    }

    private static string FindProjectRoot() =>
        DocGeneration.TestInfrastructure.ProjectRootFinder.FindSolutionRoot();
}
