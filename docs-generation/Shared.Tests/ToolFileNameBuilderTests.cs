// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace Shared.Tests;

public class ToolFileNameBuilderTests
{
    // ── Shared test fixtures ────────────────────────────────────────

    private static readonly Dictionary<string, BrandMapping> BrandMappings = new()
    {
        ["aks"] = new BrandMapping { FileName = "azure-kubernetes-service" },
        ["acr"] = new BrandMapping { FileName = "azure-container-registry" },
        ["appservice"] = new BrandMapping { FileName = "azure-app-service" },
    };

    private static readonly Dictionary<string, string> CompoundWords = new()
    {
        ["nodepool"] = "node-pool",
        ["resourcegroup"] = "resource-group",
        ["webapp"] = "web-app",
    };

    private static readonly HashSet<string> StopWords = new() { "azure" };

    private static readonly FileNameContext Ctx = new(BrandMappings, CompoundWords, StopWords);

    // ── BuildBaseFileName ──────────────────────────────────────────

    [Fact]
    public void BuildBaseFileName_BrandMapped_ReturnsCorrectPrefix()
    {
        var result = ToolFileNameBuilder.BuildBaseFileName("aks nodepool get", Ctx);
        Assert.Equal("azure-kubernetes-service-node-pool-get", result);
    }

    [Fact]
    public void BuildBaseFileName_CompoundWordArea_ExpandsAndAddsAzurePrefix()
    {
        // "nodepool" is in compound words → "node-pool", gets "azure-" prefix
        var result = ToolFileNameBuilder.BuildBaseFileName("nodepool list", Ctx);
        Assert.Equal("azure-node-pool-list", result);
    }

    [Fact]
    public void BuildBaseFileName_UnknownArea_UsesRawAreaWithAzurePrefix()
    {
        var result = ToolFileNameBuilder.BuildBaseFileName("storage list", Ctx);
        Assert.Equal("azure-storage-list", result);
    }

    [Fact]
    public void BuildBaseFileName_AreaOnly_ReturnsPrefixOnly()
    {
        var result = ToolFileNameBuilder.BuildBaseFileName("aks", Ctx);
        Assert.Equal("azure-kubernetes-service", result);
    }

    [Fact]
    public void BuildBaseFileName_AreaAlreadyHasAzurePrefix_DoesNotDouble()
    {
        var mappings = new Dictionary<string, BrandMapping>
        {
            ["cosmos"] = new BrandMapping { FileName = "azure-cosmos-db" },
        };
        var ctx = new FileNameContext(mappings, CompoundWords, StopWords);
        var result = ToolFileNameBuilder.BuildBaseFileName("cosmos get", ctx);
        Assert.Equal("azure-cosmos-db-get", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildBaseFileName_NullOrWhitespace_ReturnsUnknown(string? command)
    {
        var result = ToolFileNameBuilder.BuildBaseFileName(command!, Ctx);
        Assert.Equal("unknown", result);
    }

    [Fact]
    public void BuildBaseFileName_CompoundWordInRemainingParts_Expanded()
    {
        // "aks nodepool get" → brand=azure-kubernetes-service, remaining="nodepool" expanded to "node-pool"
        var result = ToolFileNameBuilder.BuildBaseFileName("aks nodepool get", Ctx);
        Assert.Contains("node-pool", result);
    }

    [Fact]
    public void BuildBaseFileName_StopWordsRemoved()
    {
        // "azure" is a stop word, should be stripped from remaining parts
        var result = ToolFileNameBuilder.BuildBaseFileName("acr azure thing", Ctx);
        Assert.Equal("azure-container-registry-thing", result);
    }

    [Fact]
    public void BuildBaseFileName_IsDeterministic()
    {
        var a = ToolFileNameBuilder.BuildBaseFileName("aks nodepool get", Ctx);
        var b = ToolFileNameBuilder.BuildBaseFileName("aks nodepool get", Ctx);
        Assert.Equal(a, b);
    }

    [Fact]
    public void BuildBaseFileName_RemainingPartsCaseLowered()
    {
        // Brand mapping keys are case-sensitive (CLI always sends lowercase),
        // but remaining parts are lowercased by the builder
        var result = ToolFileNameBuilder.BuildBaseFileName("aks NODEPOOL GET", Ctx);
        Assert.Equal("azure-kubernetes-service-node-pool-get", result);
    }

    [Fact]
    public void BuildBaseFileName_AreaCaseSensitiveForBrandMapping()
    {
        // Brand mapping lookup is case-sensitive; uppercase area misses the mapping
        var result = ToolFileNameBuilder.BuildBaseFileName("AKS nodepool get", Ctx);
        Assert.Equal("azure-aks-node-pool-get", result);
    }

    // ── Typed filename builders ────────────────────────────────────

    [Fact]
    public void BuildAnnotationFileName_AppendsSuffix()
    {
        var result = ToolFileNameBuilder.BuildAnnotationFileName("aks nodepool get", Ctx);
        Assert.Equal("azure-kubernetes-service-node-pool-get-annotations.md", result);
    }

    [Fact]
    public void BuildParameterFileName_AppendsSuffix()
    {
        var result = ToolFileNameBuilder.BuildParameterFileName("aks nodepool get", Ctx);
        Assert.Equal("azure-kubernetes-service-node-pool-get-parameters.md", result);
    }

    [Fact]
    public void BuildExamplePromptsFileName_AppendsSuffix()
    {
        var result = ToolFileNameBuilder.BuildExamplePromptsFileName("aks nodepool get", Ctx);
        Assert.Equal("azure-kubernetes-service-node-pool-get-example-prompts.md", result);
    }

    [Fact]
    public void BuildInputPromptFileName_AppendsSuffix()
    {
        var result = ToolFileNameBuilder.BuildInputPromptFileName("aks nodepool get", Ctx);
        Assert.Equal("azure-kubernetes-service-node-pool-get-input-prompt.md", result);
    }

    [Fact]
    public void BuildRawOutputFileName_AppendsSuffix()
    {
        var result = ToolFileNameBuilder.BuildRawOutputFileName("aks nodepool get", Ctx);
        Assert.Equal("azure-kubernetes-service-node-pool-get-raw-output.txt", result);
    }

    [Fact]
    public void BuildToolFileName_HasMdExtensionOnly()
    {
        var result = ToolFileNameBuilder.BuildToolFileName("aks nodepool get", Ctx);
        Assert.Equal("azure-kubernetes-service-node-pool-get.md", result);
    }

    // ── Explicit params overloads produce same results ─────────────

    [Fact]
    public void ExplicitParams_MatchesContextOverload()
    {
        var fromCtx = ToolFileNameBuilder.BuildBaseFileName("aks nodepool get", Ctx);
        var fromParams = ToolFileNameBuilder.BuildBaseFileName(
            "aks nodepool get", BrandMappings, CompoundWords, StopWords);
        Assert.Equal(fromCtx, fromParams);
    }

    // ── FileNameContext ────────────────────────────────────────────

    [Fact]
    public void FileNameContext_StoresAllProperties()
    {
        var ctx = new FileNameContext(BrandMappings, CompoundWords, StopWords);
        Assert.Same(BrandMappings, ctx.BrandMappings);
        Assert.Same(CompoundWords, ctx.CompoundWords);
        Assert.Same(StopWords, ctx.StopWords);
    }

    // ── Edge cases ─────────────────────────────────────────────────

    [Fact]
    public void BuildBaseFileName_MultipleSpaces_HandledCorrectly()
    {
        var result = ToolFileNameBuilder.BuildBaseFileName("aks  nodepool  get", Ctx);
        Assert.Equal("azure-kubernetes-service-node-pool-get", result);
    }

    [Fact]
    public void BuildBaseFileName_NoCompoundWordsOrBrandMapping_FallsBack()
    {
        var emptyCtx = new FileNameContext(
            new Dictionary<string, BrandMapping>(),
            new Dictionary<string, string>(),
            new HashSet<string>());
        var result = ToolFileNameBuilder.BuildBaseFileName("mysvc action do", emptyCtx);
        Assert.Equal("azure-mysvc-action-do", result);
    }

    [Fact]
    public void BuildBaseFileName_BrandMappingWithEmptyFileName_FallsThrough()
    {
        var mappings = new Dictionary<string, BrandMapping>
        {
            ["svc"] = new BrandMapping { FileName = "" },
        };
        var ctx = new FileNameContext(mappings, CompoundWords, StopWords);
        var result = ToolFileNameBuilder.BuildBaseFileName("svc list", ctx);
        // Empty FileName → falls through to compound words → raw area
        Assert.Equal("azure-svc-list", result);
    }
}
