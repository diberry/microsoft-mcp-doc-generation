// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests documenting that FamilyFileStitcher.Stitch() does NOT apply
/// contraction expansion, MCP/VM acronym expansion, or example-value backtick
/// wrapping. These are now expected to be handled upstream by the Step 3 AI
/// prompts (system-prompt.txt style guide). Removing them from Stitch keeps the
/// post-processing pipeline lean and avoids double-handling.
/// PRD-QUALITY Item A.
/// </summary>
public class StitcherStep3PromptHandlingTests
{
    private static FamilyContent MakeContent(string toolBody)
    {
        return new FamilyContent
        {
            FamilyName = "test",
            Metadata = "---\ntitle: Test\n---\n\n# Test\n\nIntro paragraph.",
            Tools =
            [
                new ToolContent
                {
                    ToolName = "test-tool", FileName = "test.md", FamilyName = "test",
                    Content = toolBody
                }
            ],
            RelatedContent = "## Related content"
        };
    }

    // ── Contractions: Step 3 AI handles these; Stitch must not double-apply ──

    [Fact]
    public void Stitch_DoesNotApplyContractions_UpstreamAIHandlesContractions()
    {
        // AI-generated content that already contains expanded forms should pass through
        // unchanged. Stitch must not mutate "does not" → "doesn't".
        var content = MakeContent("## List resources\n<!-- @mcpcli test list -->\n\nThis tool does not support filtering. It is not available in all regions.");

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        // Stitch must NOT contract these — Step 3 AI is responsible
        Assert.Contains("does not support", result);
        Assert.Contains("is not available", result);
        Assert.DoesNotContain("doesn't support", result);
        Assert.DoesNotContain("isn't available", result);
    }

    [Fact]
    public void Stitch_DoesNotApplyPositiveContractions_UpstreamAIHandles()
    {
        var content = MakeContent("## Get resource\n<!-- @mcpcli test get -->\n\nIt is ready. You are authenticated.");

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        // Stitch must NOT contract positive forms — Step 3 AI is responsible
        Assert.Contains("It is ready", result);
        Assert.Contains("You are authenticated", result);
        Assert.DoesNotContain("It's ready", result);
        Assert.DoesNotContain("You're authenticated", result);
    }

    // ── MCP acronym: Step 3 AI handles first-use expansion; Stitch must not re-expand ──

    [Fact]
    public void Stitch_DoesNotExpandMcpAcronym_UpstreamAIHandlesExpansion()
    {
        // If the AI already expanded "MCP" on first mention, Stitch must not
        // touch it again. If the AI omitted expansion, Stitch must not add it —
        // that is a prompt quality issue for Step 3 to address.
        var content = MakeContent("## List accounts\n<!-- @mcpcli storage account list -->\n\nUse the Azure MCP Server to list storage accounts.");

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        // Stitch must NOT expand MCP acronym — Step 3 AI is responsible
        // "Azure MCP Server" should remain exactly as produced by upstream steps
        var mcpExpansionCount = CountOccurrences(result, "Model Context Protocol (MCP)");
        Assert.Equal(0, mcpExpansionCount);
    }

    [Fact]
    public void Stitch_DoesNotExpandVMAcronym_UpstreamAIHandlesExpansion()
    {
        var content = MakeContent("## List VMs\n<!-- @mcpcli compute vm list -->\n\nList all VM instances in the subscription.");

        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        // Stitch must NOT expand VM acronym — Step 3 AI is responsible
        Assert.DoesNotContain("virtual machine (VM)", result);
    }

    // ── Backtick wrapping: Step 3 AI handles example-value formatting ──

    [Fact]
    public void Stitch_DoesNotWrapBareExampleValues_UpstreamAIHandlesFormatting()
    {
        var toolContent = string.Join("\n",
            "## Database get",
            "<!-- @mcpcli appservice database get -->",
            "",
            "Gets a database connection.",
            "",
            "| Parameter | Required or optional | Description |",
            "|-----------|---------------------|-------------|",
            "| **Server** | Required | FQDN of the server (for example, myserver.database.windows.net). |");

        var content = MakeContent(toolContent);
        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        // Stitch must NOT wrap bare example values — Step 3 AI is responsible
        // The bare value should pass through unchanged
        Assert.Contains("(for example, myserver.database.windows.net)", result);
        Assert.DoesNotContain("(for example, `myserver.database.windows.net`)", result);
    }

    [Fact]
    public void Stitch_AlreadyBacktickedExampleValues_PassThroughUnchanged()
    {
        // If AI already backticked example values, Stitch must leave them alone (idempotent pass-through)
        var toolContent = string.Join("\n",
            "## Metric list",
            "<!-- @mcpcli monitor metric list -->",
            "",
            "Lists metrics.",
            "",
            "| Parameter | Required or optional | Description |",
            "|-----------|---------------------|-------------|",
            "| **Interval** | Optional | Time interval (for example, `PT1H` for 1 hour). |");

        var content = MakeContent(toolContent);
        var stitcher = new FamilyFileStitcher();
        var result = stitcher.Stitch(content);

        // Already-backticked values must remain unchanged (no double-wrapping)
        Assert.Contains("(for example, `PT1H` for 1 hour)", result);
        Assert.DoesNotContain("``PT1H``", result);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
