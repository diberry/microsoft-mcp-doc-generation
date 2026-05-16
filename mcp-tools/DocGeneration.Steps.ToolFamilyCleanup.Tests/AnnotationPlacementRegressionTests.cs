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
    private static readonly string FixturesDir = GetFixturesDir();
    private static readonly string RepoRoot = FindRepoRoot();

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
    public void R_AP1_AnnotationAppearsOncePerTool_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        var toolSections = GetToolSections(content);

        foreach (var section in toolSections)
        {
            var annotationCount = Regex.Matches(section,
                @"\[Tool annotation hints\]\(index\.md#tool-annotations-for-azure-mcp-server\):").Count;
            Assert.True(annotationCount <= 1,
                $"Tool section has {annotationCount} annotation blocks (expected 0 or 1)");
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
    public void R_AP4_AnnotationLinkFormat_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
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

    // ── R-AP6: Annotation content uses emoji-pair format ─────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_AP6_AnnotationContentUsesEmojiPairFormat()
    {
        var content = LoadFixture("ValidToolFamily.md");

        // Find annotation value lines (lines after the annotation link)
        var annotationMatches = Regex.Matches(content,
            @"\[Tool annotation hints\]\(index\.md#tool-annotations-for-azure-mcp-server\):\r?\n\r?\n(.+)",
            RegexOptions.Multiline);

        Assert.NotEmpty(annotationMatches);
        foreach (Match match in annotationMatches)
        {
            var valueLine = match.Groups[1].Value.TrimEnd('\r');
            // Must contain pipe-separated emoji pairs like "Key: ✅ | Key: ❌"
            Assert.Matches(@"^(\w[\w\s]*:\s[✅❌]\s*\|\s*)*\w[\w\s]*:\s[✅❌]$", valueLine);
        }
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_AP6_AnnotationContentUsesEmojiPairFormat_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");

        var annotationMatches = Regex.Matches(content,
            @"\[Tool annotation hints\]\(index\.md#tool-annotations-for-azure-mcp-server\):\r?\n\r?\n(.+)",
            RegexOptions.Multiline);

        Assert.NotEmpty(annotationMatches);
        foreach (Match match in annotationMatches)
        {
            var valueLine = match.Groups[1].Value.TrimEnd('\r');
            // Each pair: "Key: ✅" or "Key: ❌" separated by " | "
            var pairs = valueLine.Split('|').Select(p => p.Trim()).ToArray();
            foreach (var pair in pairs)
            {
                Assert.Matches(@"^[\w\s]+:\s[✅❌]$", pair);
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static int FindTabSeparatorIndex(string section)
    {
        var lines = section.Split('\n');
        bool inCodeBlock = false;
        int charIndex = 0;
        bool passedCliTab = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("```")) inCodeBlock = !inCodeBlock;
            if (trimmed.Contains("#### [Azure MCP CLI](#tab/azure-mcp-cli)")) passedCliTab = true;
            if (!inCodeBlock && trimmed == "---" && passedCliTab)
                return charIndex;
            charIndex += line.Length + 1; // +1 for \n
        }
        return -1;
    }

    private static List<string> GetToolSections(string content)
    {
        var sections = new List<string>();
        var h2Matches = Regex.Matches(content, @"^## .+$", RegexOptions.Multiline);

        for (int i = 0; i < h2Matches.Count; i++)
        {
            var heading = h2Matches[i].Groups[0].Value;
            if (heading.Contains("Related content")) continue;

            var start = h2Matches[i].Index;
            var end = (i + 1 < h2Matches.Count) ? h2Matches[i + 1].Index : content.Length;
            sections.Add(content[start..end]);
        }
        return sections;
    }

    private static string LoadFixture(string filename)
    {
        var path = Path.Combine(FixturesDir, filename);
        Assert.True(File.Exists(path), $"Fixture not found: {path}");
        return File.ReadAllText(path);
    }

    private static string LoadRealGeneratedFile(params string[] pathParts)
    {
        var path = Path.Combine(new[] { RepoRoot }.Concat(pathParts).ToArray());
        Assert.True(File.Exists(path), $"Generated file not found: {path}. Run generation first.");
        return File.ReadAllText(path);
    }

    private static string GetFixturesDir()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "Fixtures");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }
        return Path.Combine(FindRepoRoot(), "mcp-tools", "DocGeneration.Steps.ToolFamilyCleanup.Tests", "Fixtures");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
            if (File.Exists(Path.Combine(dir, "mcp-doc-generation.sln")))
                return dir;
        }
        throw new InvalidOperationException("Could not find repo root (mcp-doc-generation.sln)");
    }
}
