// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Regression tests for annotation placement rules (R-AP1 through R-AP6).
/// Validates annotations appear once per tool, after --- separator, outside tabs,
/// with correct link format and emoji-pair content format.
/// </summary>
public class AnnotationPlacementRegressionTests
{
    private static string FixturesDir => RegressionTestHelpers.FixturesDir;
    private static string RepoRoot => RegressionTestHelpers.RepoRoot;

    // ── R-AP1: Annotation appears once per tool H2 ───────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_AP1_AnnotationAppearsOncePerTool()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var toolSections = GetToolSections(content);

        foreach (var section in toolSections)
        {
            var annotationCount = Regex.Matches(section,
                @"\[Tool annotation hints\]\(index\.md#tool-annotations-for-azure-mcp-server\):").Count;
            Assert.True(annotationCount <= 1,
                $"Tool section has {annotationCount} annotation blocks (expected 0 or 1)");
        }
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    [Trait("Category", "RequiresGeneration")]
    public void R_AP1_AnnotationAppearsOncePerTool_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        if (content == null) return;
        var toolSections = GetToolSections(content);

        foreach (var section in toolSections)
        {
            var annotationCount = Regex.Matches(section,
                @"\[Tool annotation hints\]\(index\.md#tool-annotations-for-azure-mcp-server\):").Count;
            Assert.Equal(1, annotationCount);
        }
    }

    // ── R-AP2: Annotations appear after --- separator ────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_AP2_AnnotationAfterSeparator()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var toolSections = GetToolSections(content);

        foreach (var section in toolSections)
        {
            var annotationIndex = section.IndexOf("[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):", StringComparison.Ordinal);
            if (annotationIndex < 0) continue;

            // Find the tab-closing --- separator (outside code blocks)
            var separatorIndex = FindTabSeparatorIndex(section);
            Assert.True(separatorIndex >= 0, "Tab separator --- not found in section with annotations");
            Assert.True(annotationIndex > separatorIndex,
                "Annotation must appear AFTER the --- tab separator");
        }
    }

    // ── R-AP3: Annotations appear outside tabs ───────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_AP3_AnnotationOutsideTabs()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var toolSections = GetToolSections(content);

        foreach (var section in toolSections)
        {
            var annotationIndex = section.IndexOf("[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):", StringComparison.Ordinal);
            if (annotationIndex < 0) continue;

            // Annotations must NOT be between MCP tab header and the --- separator
            var mcpTabIndex = section.IndexOf("#### [MCP Server](#tab/mcp-server)", StringComparison.Ordinal);
            var separatorIndex = FindTabSeparatorIndex(section);

            if (mcpTabIndex >= 0 && separatorIndex >= 0)
            {
                Assert.False(annotationIndex > mcpTabIndex && annotationIndex < separatorIndex,
                    "Annotation must not appear inside tab content (between tab header and ---)");
            }
        }
    }

    // ── R-AP4: Annotation link format ────────────────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_AP4_AnnotationLinkFormat()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var annotationLinks = Regex.Matches(content,
            @"\[Tool annotation hints\]\([^\)]+\):");

        foreach (Match link in annotationLinks)
        {
            Assert.Equal("[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):", link.Value);
        }
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    [Trait("Category", "RequiresGeneration")]
    public void R_AP4_AnnotationLinkFormat_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        if (content == null) return;
        var annotationLinks = Regex.Matches(content,
            @"\[Tool annotation hints\]\([^\)]+\):");

        Assert.True(annotationLinks.Count > 0, "No annotation links found in real file");
        foreach (Match link in annotationLinks)
        {
            Assert.Equal("[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):", link.Value);
        }
    }

    // ── R-AP5: No annotation block when tool has no annotations ──────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_AP5_NoAnnotationBlockWhenEmpty()
    {
        var content = LoadFixture("ToolWithNoAnnotations.md");
        Assert.DoesNotContain("[Tool annotation hints]", content);
    }

    // ── R-AP6: Annotation content uses 3-row markdown table format ───────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_AP6_AnnotationContentUsesTableFormat()
    {
        var content = LoadFixture("ValidToolFamily.md");

        // Each annotation block must contain the header row
        var headerMatches = Regex.Matches(content,
            @"\| Destructive \| Idempotent \| Open World \| Read Only \| Secret \| Local Required \|",
            RegexOptions.Multiline);
        Assert.NotEmpty(headerMatches);

        // Each annotation block must contain the centered-alignment separator row
        var separatorMatches = Regex.Matches(content,
            @"\|:-----------:\|:----------:\|:----------:\|:---------:\|:------:\|:--------------:\|",
            RegexOptions.Multiline);
        Assert.NotEmpty(separatorMatches);

        // Values rows must use only ✅ or ❌ cells (no "Key: emoji" inline pairs)
        var valueRowMatches = Regex.Matches(content,
            @"^\|\s*(✅|❌)\s*\|",
            RegexOptions.Multiline);
        Assert.NotEmpty(valueRowMatches);
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    [Trait("Category", "RequiresGeneration")]
    public void R_AP6_AnnotationContentUsesTableFormat_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        if (content == null) return;

        // Must contain the table separator with centered alignment colons
        Assert.Matches(
            @"\|:-----------:\|:----------:\|:----------:\|:---------:\|:------:\|:--------------:\|",
            content);
    }

    // ── R-AP7: Old inline format MUST NOT appear in any assembled article ─

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_AP7_NoInlineAnnotationFormatInFixture()
    {
        var content = LoadFixture("ValidToolFamily.md");

        // The old inline format is a line starting with "Destructive: ✅" or "Destructive: ❌"
        // followed by pipe-separated "Key: emoji" pairs.
        // This guard FAILS if that format ever appears in assembled output.
        Assert.DoesNotMatch(
            @"(?m)^\s*Destructive:\s*(✅|❌)\s*\|",
            content);
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    [Trait("Category", "RequiresGeneration")]
    public void R_AP7_NoInlineAnnotationFormat_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        if (content == null) return;

        Assert.DoesNotMatch(
            @"(?m)^\s*Destructive:\s*(✅|❌)\s*\|",
            content);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static int FindTabSeparatorIndex(string section)
        => RegressionTestHelpers.FindTabSeparatorIndex(section);

    private static List<string> GetToolSections(string content)
        => RegressionTestHelpers.GetToolSections(content);

    private static string LoadFixture(string filename)
        => RegressionTestHelpers.LoadFixture(filename);

    private static string? LoadRealGeneratedFile(params string[] pathParts)
        => RegressionTestHelpers.TryLoadRealGeneratedFile(pathParts);
}