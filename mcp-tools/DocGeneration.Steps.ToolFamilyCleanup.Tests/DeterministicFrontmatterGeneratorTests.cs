// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

public class DeterministicFrontmatterGeneratorTests
{
    private const string TestBrandName = "Azure Storage";
    private const string TestSeoDescription = "Use Azure MCP Server tools to manage Azure Storage resources such as storage accounts, blob containers, blobs, and tables with natural language prompts from your IDE.";
    private const int TestToolCount = 7;
    private const string TestCliVersion = "2.0.0-beta.31+ed24dd9783f26645fd2b7218b4d52221b446354f";

    // ── Generate: frontmatter structure ──────────────────────────────

    [Fact]
    public void Generate_ReturnsValidYamlFrontmatter()
    {
        var result = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, TestToolCount, TestCliVersion, TestSeoDescription);

        Assert.StartsWith("---\n", result.Replace("\r\n", "\n"));
        Assert.Contains("\n---\n", result.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Generate_TitleContainsBrandName()
    {
        var result = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, TestToolCount, TestCliVersion, TestSeoDescription);

        Assert.Contains($"title: Azure MCP Server tools for {TestBrandName}", result);
    }

    [Fact]
    public void Generate_UsesProvidedSeoDescription()
    {
        var result = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, TestToolCount, TestCliVersion, TestSeoDescription);

        Assert.Contains($"description: {TestSeoDescription}", result);
    }

    [Fact]
    public void Generate_FallsBackToGenericDescription_WhenSeoDescriptionNull()
    {
        var result = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, TestToolCount, TestCliVersion, seoDescription: null);

        Assert.Contains($"description: Use Azure MCP Server tools to manage {TestBrandName} resources with natural language prompts from your IDE.", result);
    }

    [Fact]
    public void Generate_CorrectToolCount()
    {
        var result = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, TestToolCount, TestCliVersion, TestSeoDescription);

        Assert.Contains($"tool_count: {TestToolCount}", result);
    }

    [Fact]
    public void Generate_CorrectCliVersion()
    {
        var result = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, TestToolCount, TestCliVersion, TestSeoDescription);

        Assert.Contains($"mcp-cli.version: {TestCliVersion}", result);
    }

    [Fact]
    public void Generate_AlwaysSetsServiceField()
    {
        var result = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, TestToolCount, TestCliVersion, TestSeoDescription);

        Assert.Contains("ms.service: azure-mcp-server", result);
    }

    [Fact]
    public void Generate_AlwaysSetsTopicField()
    {
        var result = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, TestToolCount, TestCliVersion, TestSeoDescription);

        Assert.Contains("ms.topic: concept-article", result);
    }

    [Fact]
    public void Generate_H1MatchesTitle()
    {
        var result = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, TestToolCount, TestCliVersion, TestSeoDescription);

        Assert.Contains($"# Azure MCP Server tools for {TestBrandName}", result);
    }

    [Fact]
    public void Generate_DifferentBrandNames_ProduceDifferentOutput()
    {
        var storage = DeterministicFrontmatterGenerator.Generate(
            "Azure Storage", 7, TestCliVersion, "Storage desc.");
        var keyvault = DeterministicFrontmatterGenerator.Generate(
            "Azure Key Vault", 5, TestCliVersion, "Key Vault desc.");

        Assert.Contains("Azure Storage", storage);
        Assert.Contains("Azure Key Vault", keyvault);
        Assert.DoesNotContain("Azure Key Vault", storage);
    }

    // ── Generate: idempotent ────────────────────────────────────────

    [Fact]
    public void Generate_SameInput_SameOutput()
    {
        var result1 = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, TestToolCount, TestCliVersion, TestSeoDescription);
        var result2 = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, TestToolCount, TestCliVersion, TestSeoDescription);

        Assert.Equal(result1, result2);
    }

    // ── ExtractIntroParagraphs ──────────────────────────────────────

    [Fact]
    public void ExtractIntroParagraphs_ExtractsBetweenH1AndInclude()
    {
        var aiMetadata = @"---
title: Azure MCP Server tools for Azure Storage
description: Use Azure MCP Server tools to manage storage.
ms.service: azure-mcp-server
ms.topic: concept-article
tool_count: 7
mcp-cli.version: 2.0.0-beta.31
---

# Azure MCP Server tools for Azure Storage

The Azure MCP Server lets you manage Azure Storage resources, including creating and configuring storage accounts.

Azure Storage is a scalable, fully managed cloud storage platform; for more information, see [Azure Storage documentation](/azure/storage/).

[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]";

        var intros = DeterministicFrontmatterGenerator.ExtractIntroParagraphs(aiMetadata);

        Assert.Contains("The Azure MCP Server lets you manage Azure Storage resources", intros);
        Assert.Contains("Azure Storage is a scalable", intros);
        Assert.DoesNotContain("[!INCLUDE", intros);
        Assert.DoesNotContain("---", intros);
        Assert.DoesNotContain("# Azure MCP Server tools", intros);
    }

    [Fact]
    public void ExtractIntroParagraphs_HandlesNoInclude()
    {
        var aiMetadata = @"---
title: Test
---

# Test heading

First paragraph.

Second paragraph.";

        var intros = DeterministicFrontmatterGenerator.ExtractIntroParagraphs(aiMetadata);

        Assert.Contains("First paragraph.", intros);
        Assert.Contains("Second paragraph.", intros);
    }

    [Fact]
    public void ExtractIntroParagraphs_ReturnsEmptyForNoContent()
    {
        var aiMetadata = @"---
title: Test
---

# Test heading

[!INCLUDE [tip](../includes/tools/parameter-consideration.md)]";

        var intros = DeterministicFrontmatterGenerator.ExtractIntroParagraphs(aiMetadata);

        Assert.Equal(string.Empty, intros);
    }

    // ── Assemble ────────────────────────────────────────────────────

    [Fact]
    public void Assemble_CombinesFrontmatterIntrosAndInclude()
    {
        var header = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, TestToolCount, TestCliVersion, TestSeoDescription);
        var intros = "First paragraph.\n\nSecond paragraph.";

        var result = DeterministicFrontmatterGenerator.Assemble(header, intros);

        // Verify order: frontmatter → H1 → intros → INCLUDE
        var normalized = result.Replace("\r\n", "\n");
        var h1Pos = normalized.IndexOf($"# Azure MCP Server tools for {TestBrandName}");
        var introPos = normalized.IndexOf("First paragraph.");
        var includePos = normalized.IndexOf("[!INCLUDE");

        Assert.True(h1Pos < introPos, "H1 should come before intros");
        Assert.True(introPos < includePos, "Intros should come before INCLUDE");
    }

    [Fact]
    public void Assemble_IncludesParameterTipInclude()
    {
        var header = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, TestToolCount, TestCliVersion, TestSeoDescription);

        var result = DeterministicFrontmatterGenerator.Assemble(header, "Some intro.");

        Assert.Contains("[!INCLUDE [tip-about-params](../includes/tools/parameter-consideration.md)]", result);
    }

    [Fact]
    public void Assemble_HandlesEmptyIntros()
    {
        var header = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, TestToolCount, TestCliVersion, TestSeoDescription);

        var result = DeterministicFrontmatterGenerator.Assemble(header, string.Empty);

        Assert.Contains("# Azure MCP Server tools for", result);
        Assert.Contains("[!INCLUDE", result);
    }

    // ── Integration: production service-doc-links.json ──────────────

    [Fact]
    public void ProductionLinks_AllValidatedNamespacesHaveSeoDescription()
    {
        var root = FindProjectRoot();
        var path = Path.Combine(root, "mcp-tools", "data", "service-doc-links.json");
        if (!File.Exists(path)) return; // Skip if not in full repo

        var links = DeterministicRelatedContentGenerator.LoadServiceDocLinks(path);

        // All validated namespaces should have seoDescription
        var validatedNamespaces = new[]
        {
            "advisor", "applens", "appservice", "cloudarchitect", "compute",
            "containerapps", "cosmos", "deploy", "fileshares", "foundryextensions",
            "group", "keyvault", "monitor", "postgres", "pricing",
            "resourcehealth", "search", "sql", "storage",
            "wellarchitectedframework", "workbooks"
        };

        foreach (var ns in validatedNamespaces)
        {
            Assert.True(links.ContainsKey(ns), $"Missing namespace: {ns}");
            Assert.False(
                string.IsNullOrWhiteSpace(links[ns].SeoDescription),
                $"Namespace '{ns}' is missing seoDescription in service-doc-links.json");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string FindProjectRoot() =>
        DocGeneration.TestInfrastructure.ProjectRootFinder.FindSolutionRoot();
}
