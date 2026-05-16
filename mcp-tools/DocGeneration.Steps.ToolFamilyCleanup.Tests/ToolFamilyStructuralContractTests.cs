// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Regression tests for tool-family document structure (R-DS1 through R-DS5).
/// Validates frontmatter, H1 pattern, H2-per-tool, and deterministic ordering.
/// </summary>
public class ToolFamilyStructuralContractTests
{
    private static string FixturesDir => RegressionTestHelpers.FixturesDir;
    private static string RepoRoot => RegressionTestHelpers.RepoRoot;

    // ── R-DS1: File begins with YAML frontmatter delimited by --- ──────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_DS1_ValidFrontmatterDelimiters_SyntheticFixture()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var lines = content.Split('\n');

        Assert.Equal("---", lines[0].TrimEnd('\r'));

        // Find closing delimiter (must exist after first line)
        var closingIndex = -1;
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd('\r') == "---")
            {
                closingIndex = i;
                break;
            }
        }
        Assert.True(closingIndex > 1, "Closing frontmatter delimiter '---' not found");
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    [Trait("Category", "RequiresGeneration")]
    public void R_DS1_ValidFrontmatterDelimiters_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        var lines = content.Split('\n');

        Assert.Equal("---", lines[0].TrimEnd('\r'));

        var closingIndex = -1;
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd('\r') == "---")
            {
                closingIndex = i;
                break;
            }
        }
        Assert.True(closingIndex > 1, "Closing frontmatter delimiter '---' not found in azure-backup.md");
    }

    // ── R-DS2: Frontmatter contains required fields ───────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_DS2_RequiredFrontmatterFields_SyntheticFixture()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var frontmatter = ExtractFrontmatter(content);

        Assert.Contains("title:", frontmatter);
        Assert.Contains("description:", frontmatter);
        Assert.Contains("ms.service:", frontmatter);
        Assert.Contains("ms.topic:", frontmatter);
        Assert.Contains("tool_count:", frontmatter);
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    [Trait("Category", "RequiresGeneration")]
    public void R_DS2_RequiredFrontmatterFields_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        var frontmatter = ExtractFrontmatter(content);

        Assert.Contains("title:", frontmatter);
        Assert.Contains("description:", frontmatter);
        Assert.Contains("ms.service:", frontmatter);
        Assert.Contains("ms.topic:", frontmatter);
        Assert.Contains("tool_count:", frontmatter);
    }

    // ── R-DS3: First H1 heading matches brand pattern ─────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_DS3_H1MatchesBrandPattern_SyntheticFixture()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var h1Match = Regex.Match(content, @"^# (.+)$", RegexOptions.Multiline);

        Assert.True(h1Match.Success, "No H1 heading found");
        Assert.Matches(@"^# Azure MCP Server tools for .+$", h1Match.Value);
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    [Trait("Category", "RequiresGeneration")]
    public void R_DS3_H1MatchesBrandPattern_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        var h1Match = Regex.Match(content, @"^# (.+)$", RegexOptions.Multiline);

        Assert.True(h1Match.Success, "No H1 heading found in azure-backup.md");
        Assert.Matches(@"^# Azure MCP Server tools for .+$", h1Match.Value);
    }

    // ── R-DS4: Each tool has exactly one H2 heading ──────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_DS4_OneH2PerTool_SyntheticFixture()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var h2Matches = Regex.Matches(content, @"^## .+$", RegexOptions.Multiline);

        // Fixture has 2 tools + "Related content" = 3 H2s
        Assert.True(h2Matches.Count >= 2, "Expected at least 2 H2 headings (tools)");

        // Each tool H2 should be unique (no duplicate headings for same tool)
        var toolH2s = h2Matches.Cast<Match>()
            .Select(m => m.Value)
            .Where(v => !v.Contains("Related content"))
            .ToList();
        Assert.Equal(toolH2s.Count, toolH2s.Distinct().Count());
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    [Trait("Category", "RequiresGeneration")]
    public void R_DS4_OneH2PerTool_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        var h2Matches = Regex.Matches(content, @"^## .+$", RegexOptions.Multiline);

        var toolH2s = h2Matches.Cast<Match>()
            .Select(m => m.Value)
            .Where(v => !v.Contains("Related content"))
            .ToList();

        // Each tool H2 must be unique
        Assert.Equal(toolH2s.Count, toolH2s.Distinct().Count());
        Assert.True(toolH2s.Count >= 2, "Expected at least 2 tool H2s in azure-backup.md");

        // Validate H2 count matches frontmatter tool_count
        var frontmatter = RegressionTestHelpers.ExtractFrontmatter(content);
        var toolCountMatch = Regex.Match(frontmatter, @"tool_count:\s*(\d+)");
        if (toolCountMatch.Success)
        {
            var expectedCount = int.Parse(toolCountMatch.Groups[1].Value);
            Assert.Equal(expectedCount, toolH2s.Count);
        }
    }

    // ── R-DS5: Tools in deterministic order ──────────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_DS5_ToolsInDeterministicOrder_SyntheticFixture()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var toolH2s = Regex.Matches(content, @"^## (.+)$", RegexOptions.Multiline)
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .Where(v => v != "Related content")
            .ToList();

        // Verify alphabetical order
        var sorted = toolH2s.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted, toolH2s);
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    [Trait("Category", "RequiresGeneration")]
    public void R_DS5_ToolsInDeterministicOrder_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        var toolH2s = Regex.Matches(content, @"^## (.+)$", RegexOptions.Multiline)
            .Cast<Match>()
            .Select(m => m.Groups[1].Value.TrimEnd('\r'))
            .Where(v => v != "Related content")
            .ToList();

        var sorted = toolH2s.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted, toolH2s);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string ExtractFrontmatter(string content)
        => RegressionTestHelpers.ExtractFrontmatter(content);

    private static string LoadFixture(string filename)
        => RegressionTestHelpers.LoadFixture(filename);

    private static string LoadRealGeneratedFile(params string[] pathParts)
        => RegressionTestHelpers.LoadRealGeneratedFile(pathParts);
}
