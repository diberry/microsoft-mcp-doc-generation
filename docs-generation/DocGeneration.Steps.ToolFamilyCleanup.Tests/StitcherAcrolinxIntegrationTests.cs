// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Integration tests verifying the full FamilyFileStitcher post-processing
/// chain applies all Acrolinx fixers in the correct order.
/// Tests would FAIL if any new fixer were removed from the chain.
/// Fixes: #215
/// </summary>
public class StitcherAcrolinxIntegrationTests
{
    private FamilyContent CreateTestContent(string metadata, string toolContent, string relatedContent = "## Related content")
    {
        return new FamilyContent
        {
            FamilyName = "test",
            Metadata = metadata,
            Tools = [new ToolContent { ToolName = "test-tool", FileName = "test.md", FamilyName = "test", Content = toolContent }],
            RelatedContent = relatedContent
        };
    }

    // ── AcronymExpander is applied (replaces old MCP-only expander) ──

    [Fact]
    public void Stitch_ExpandsVMAcronymOnFirstUse()
    {
        var content = CreateTestContent(
            "---\ntitle: Azure MCP Server tools for Compute\n---\n\n# Azure MCP Server tools for Compute\n\nThe Azure MCP Server manages VM resources.",
            "## List VMs\n\nList all VM instances in the subscription. Each VM has a unique ID.");

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        // AcronymExpander should expand VM on first body occurrence
        Assert.Contains("virtual machine (VM)", result);
    }

    [Fact]
    public void Stitch_ExpandsMcpAcronymOnFirstUse()
    {
        var content = CreateTestContent(
            "---\ntitle: Azure MCP Server tools for Storage\n---\n\n# Azure MCP Server tools for Storage\n\nThe Azure MCP Server manages storage.",
            "## List accounts\n\nUse Azure MCP Server to list accounts.");

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        Assert.Contains("Model Context Protocol (MCP)", result);
    }

    // ── PresentTenseFixer is applied ────────────────────────────────

    [Fact]
    public void Stitch_ConvertsFutureTenseToPresent()
    {
        var content = CreateTestContent(
            "---\ntitle: Test\n---\n\n# Test\n\nIntro paragraph.",
            "## Test tool\n\nThe tool will return results. The output will be displayed.");

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        Assert.Contains("returns results", result);
        Assert.Contains("is displayed", result);
        Assert.DoesNotContain("will return", result);
        Assert.DoesNotContain("will be displayed", result);
    }

    // ── IntroductoryCommaFixer is applied ───────────────────────────

    [Fact]
    public void Stitch_InsertsCommasAfterIntroductoryPhrases()
    {
        var content = CreateTestContent(
            "---\ntitle: Test\n---\n\n# Test\n\nIntro paragraph.",
            "## Test tool\n\nFor example you can list resources. By default it returns all results.");

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        Assert.Contains("For example, you", result);
        Assert.Contains("By default, it", result);
    }

    // ── ContractionFixer still works ────────────────────────────────

    [Fact]
    public void Stitch_StillAppliesContractions()
    {
        var content = CreateTestContent(
            "---\ntitle: Test\n---\n\n# Test\n\nIntro paragraph.",
            "## Test tool\n\nThis tool does not support filtering. It is not available.");

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        Assert.Contains("doesn't", result);
        Assert.Contains("isn't", result);
    }

    // ── Full pipeline integration — all fixers in sequence ──────────

    [Fact]
    public void Stitch_FullPipeline_AllFixersApplied()
    {
        var content = CreateTestContent(
            "---\ntitle: Azure MCP Server tools for Compute\n---\n\n# Azure MCP Server tools for Compute\n\nThe Azure MCP Server manages VM resources.",
            "## List VMs\n\nThe tool will return all VM instances. For example you can filter by RBAC role. It does not support pagination.");

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        // AcronymExpander: MCP and VM expanded
        Assert.Contains("Model Context Protocol (MCP)", result);
        Assert.Contains("virtual machine (VM)", result);
        // PresentTenseFixer: "will return" → "returns"
        Assert.Contains("returns all", result);
        // IntroductoryCommaFixer: "For example you" → "For example, you"
        Assert.Contains("For example, you", result);
        // ContractionFixer: "does not" → "doesn't"
        Assert.Contains("doesn't", result);
    }

    // ── Order matters: PresentTenseFixer before ContractionFixer ────

    [Fact]
    public void Stitch_PresentTenseBeforeContractions_WillNotBe_BecomesIsnt()
    {
        var content = CreateTestContent(
            "---\ntitle: Test\n---\n\n# Test\n\nIntro.",
            "## Test\n\nThe value will not be returned.");

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        // PresentTenseFixer: "will not be" → "is not"
        // ContractionFixer: "is not" → "isn't"
        Assert.Contains("isn't", result);
        Assert.DoesNotContain("will not", result);
    }

    // ── JsonSchemaCollapser is applied (Acrolinx P1) ────────────────

    [Fact]
    public void Stitch_CollapsesInlineJsonSchema()
    {
        var toolContent = "## Generate diagram\n\n| Parameter |  Required or optional | Description |\n|-----------------------|----------------------|-------------|\n| **Raw mcp tool input** |  Required | {\n    &quot;type&quot;: &quot;object&quot;,\n    &quot;properties&quot;: {\n        &quot;workspaceFolder&quot;: {\n            &quot;type&quot;: &quot;string&quot;\n        }\n    }\n}. |";

        var content = CreateTestContent(
            "---\ntitle: Test\n---\n\n# Test\n\nIntro paragraph.",
            toolContent);

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        // JsonSchemaCollapser should replace schema with prose
        Assert.Contains("JSON object that defines the input structure for this tool.", result);
        Assert.DoesNotContain("&quot;properties&quot;", result);
        // Table structure preserved
        Assert.Contains("| **Raw mcp tool input** |", result);
    }
}
