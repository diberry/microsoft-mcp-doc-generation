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

    // ── AcronymExpander and ContractionFixer moved to Step 3 AI ────────────
    // These fixers are no longer called from FamilyFileStitcher.Stitch().
    // The Step 3 AI system prompt (system-prompt.txt) now handles:
    //   - MCP and VM acronym expansion on first body use
    //   - Contractions per Microsoft style guide
    // See StitcherStep3PromptHandlingTests for the new contract tests.

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

    // ── Contraction/acronym expansion handled by Step 3 AI, not Stitch ──
    // See StitcherStep3PromptHandlingTests for the contract tests.

    // ── Full pipeline integration — remaining fixers in sequence ────

    [Fact]
    public void Stitch_FullPipeline_DeterministicFixersApplied()
    {
        var content = CreateTestContent(
            "---\ntitle: Azure MCP Server tools for Compute\n---\n\n# Azure MCP Server tools for Compute\n\nThe Azure MCP Server manages VM resources.",
            "## List VMs\n\nThe tool will return all VM instances. For example you can filter by RBAC role. It does not support pagination.");

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        // PresentTenseFixer: "will return" → "returns"
        Assert.Contains("returns all", result);
        // IntroductoryCommaFixer: "For example you" → "For example, you"
        Assert.Contains("For example, you", result);
        // ContractionFixer and AcronymExpander are now Step 3 AI responsibilities
        // (see StitcherStep3PromptHandlingTests)
    }

    // ── Order matters: PresentTenseFixer applied; ContractionFixer is not ──

    [Fact]
    public void Stitch_PresentTenseApplied_WillNotBe_BecomesIsNot()
    {
        var content = CreateTestContent(
            "---\ntitle: Test\n---\n\n# Test\n\nIntro.",
            "## Test\n\nThe value will not be returned.");

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        // PresentTenseFixer: "will not be" → "is not"
        // ContractionFixer is no longer in the chain — "is not" stays as-is
        // (Step 3 AI handles contractions upstream)
        Assert.Contains("is not returned", result);
        Assert.DoesNotContain("will not", result);
        // Stitch does NOT further contract "is not" → "isn't"
        Assert.DoesNotContain("isn't returned", result);
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
