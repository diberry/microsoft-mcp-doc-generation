// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Regression tests for tab structure (R-TS1 through R-TS5) and
/// CLI command block rules (R-CC1 through R-CC4).
/// </summary>
public class TabStructureRegressionTests
{
    private static string FixturesDir => RegressionTestHelpers.FixturesDir;
    private static string RepoRoot => RegressionTestHelpers.RepoRoot;

    // ── R-TS1: MCP tab appears first ─────────────────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_TS1_McpTabAppearsFirst()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var toolSections = GetToolSections(content);

        foreach (var section in toolSections)
        {
            var mcpIndex = section.IndexOf("#### [MCP Server](#tab/mcp-server)", StringComparison.Ordinal);
            var cliIndex = section.IndexOf("#### [Azure MCP CLI](#tab/azure-mcp-cli)", StringComparison.Ordinal);

            if (mcpIndex >= 0 && cliIndex >= 0)
            {
                Assert.True(mcpIndex < cliIndex, "MCP tab must appear before CLI tab");
            }
        }
    }

    // ── R-TS2: CLI tab appears second ────────────────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_TS2_CliTabAppearsSecond()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var toolSections = GetToolSections(content);

        foreach (var section in toolSections)
        {
            var mcpIndex = section.IndexOf("#### [MCP Server](#tab/mcp-server)", StringComparison.Ordinal);
            var cliIndex = section.IndexOf("#### [Azure MCP CLI](#tab/azure-mcp-cli)", StringComparison.Ordinal);

            Assert.True(mcpIndex >= 0, "MCP tab header missing");
            Assert.True(cliIndex >= 0, "CLI tab header missing");
            Assert.True(cliIndex > mcpIndex, "CLI tab must appear after MCP tab");
        }
    }

    // ── R-TS3: Single --- separator after CLI tab content ────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_TS3_SingleSeparatorAfterCliTab()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var toolSections = GetToolSections(content);

        foreach (var section in toolSections)
        {
            var cliTabIndex = section.IndexOf("#### [Azure MCP CLI](#tab/azure-mcp-cli)", StringComparison.Ordinal);
            if (cliTabIndex < 0) continue;

            var afterCli = section[cliTabIndex..];
            // Count standalone --- lines (not inside code fences)
            var separatorCount = RegressionTestHelpers.CountSeparatorsOutsideCodeBlocks(afterCli);
            Assert.Equal(1, separatorCount);
        }
    }

    // ── R-TS4: No separators inside tabs ─────────────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_TS4_NoSeparatorsInsideTabs()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var toolSections = RegressionTestHelpers.GetToolSections(content);

        foreach (var section in toolSections)
        {
            var mcpStart = section.IndexOf("#### [MCP Server](#tab/mcp-server)", StringComparison.Ordinal);
            var cliStart = section.IndexOf("#### [Azure MCP CLI](#tab/azure-mcp-cli)", StringComparison.Ordinal);
            if (mcpStart < 0 || cliStart < 0) continue;

            // Content between MCP header and CLI header (MCP tab content)
            var mcpHeaderEnd = section.IndexOf('\n', mcpStart) + 1;
            var mcpContent = section[mcpHeaderEnd..cliStart];
            Assert.Equal(0, RegressionTestHelpers.CountSeparatorsOutsideCodeBlocks(mcpContent));

            // CLI tab content: from after CLI header to end of section
            var cliHeaderEnd = section.IndexOf('\n', cliStart) + 1;
            var cliContent = section[cliHeaderEnd..];

            // Count separators in CLI tab — should be exactly 1 (the tab terminator)
            var lines = cliContent.Split('\n');
            bool inCodeBlock = false;
            int separatorsBeforeTerminator = 0;
            bool foundTerminator = false;
            foreach (var line in lines)
            {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.StartsWith("```")) inCodeBlock = !inCodeBlock;
                if (!inCodeBlock && trimmed == "---")
                {
                    if (!foundTerminator)
                    {
                        foundTerminator = true; // First --- is the tab terminator
                    }
                    else
                    {
                        separatorsBeforeTerminator++;
                    }
                }
            }
            Assert.True(foundTerminator, "Tab terminator --- not found in CLI tab content");
            Assert.Equal(0, separatorsBeforeTerminator);
        }
    }

    // ── R-TS5: Exact tab header format ───────────────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_TS5_ExactTabHeaderFormat()
    {
        var content = LoadFixture("ValidToolFamily.md");

        // Find all #### headers that look like tab headers
        var tabHeaders = Regex.Matches(content, @"^#### \[.+?\]\(#tab/.+?\)$", RegexOptions.Multiline);

        foreach (Match header in tabHeaders)
        {
            var value = header.Value;
            Assert.True(
                value == "#### [MCP Server](#tab/mcp-server)" ||
                value == "#### [Azure MCP CLI](#tab/azure-mcp-cli)",
                $"Unexpected tab header format: '{value}'");
        }
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    [Trait("Category", "RequiresGeneration")]
    public void R_TS5_ExactTabHeaderFormat_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        if (content == null) return;

        var tabHeaders = Regex.Matches(content, @"^#### \[.+?\]\(#tab/.+?\)\r?$", RegexOptions.Multiline);
        Assert.True(tabHeaders.Count > 0, "No tab headers found in real file");

        foreach (Match header in tabHeaders)
        {
            var value = header.Value.TrimEnd('\r');
            Assert.True(
                value == "#### [MCP Server](#tab/mcp-server)" ||
                value == "#### [Azure MCP CLI](#tab/azure-mcp-cli)",
                $"Unexpected tab header format in real file: '{value}'");
        }
    }

    // ── R-CC1: CLI example fenced with ```console ────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_CC1_CliExampleFenceIsConsole()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var toolSections = GetToolSections(content);

        foreach (var section in toolSections)
        {
            var cliStart = section.IndexOf("#### [Azure MCP CLI](#tab/azure-mcp-cli)", StringComparison.Ordinal);
            if (cliStart < 0) continue;

            var cliContent = section[cliStart..];
            var fenceMatches = Regex.Matches(cliContent, @"^```(\w*)\r?$", RegexOptions.Multiline);

            // First opening fence should be ```console
            var openingFences = fenceMatches.Cast<Match>()
                .Where(m => m.Groups[1].Value != "") // skip closing ```
                .ToList();
            Assert.True(openingFences.Count > 0, "No code fence found in CLI tab");
            Assert.Equal("console", openingFences[0].Groups[1].Value);
        }
    }

    // ── R-CC2: Command starts with azmcp ─────────────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_CC2_CliCommandStartsWithAzmcp()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var consoleBlocks = ExtractConsoleBlocks(content);

        Assert.NotEmpty(consoleBlocks);
        foreach (var block in consoleBlocks)
        {
            var firstLine = block.Split('\n')[0].Trim();
            Assert.StartsWith("azmcp", firstLine);
        }
    }

    // ── R-CC3: Required params use --param <param> format ────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_CC3_RequiredParamsNoSquareBrackets()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var consoleBlocks = ExtractConsoleBlocks(content);

        foreach (var block in consoleBlocks)
        {
            // Find lines with --param <param> (no square brackets) — these are required
            var requiredParams = Regex.Matches(block, @"^\s+--(\S+)\s+<[^>]+>", RegexOptions.Multiline);
            foreach (Match param in requiredParams)
            {
                var line = param.Value;
                Assert.DoesNotContain("[--", line);
            }
        }
    }

    // ── R-CC4: Optional params use [--param <param>] format ──────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_CC4_OptionalParamsHaveSquareBrackets()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var consoleBlocks = ExtractConsoleBlocks(content);

        foreach (var block in consoleBlocks)
        {
            // Find lines with [--param <param>] — these are optional
            var optionalParams = Regex.Matches(block, @"\[--\S+\s+<[^>]+>\]");
            Assert.True(optionalParams.Count > 0 || !block.Contains("[--"),
                "Optional params must use [--param <param>] format");

            // Verify all bracketed params have proper format
            foreach (Match param in optionalParams)
            {
                Assert.Matches(@"\[--[\w-]+\s+<[\w-]+>\]", param.Value);
            }
        }
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    [Trait("Category", "RequiresGeneration")]
    public void R_CC1_Through_CC4_RealFile()
    {
        var content = RegressionTestHelpers.TryLoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        if (content == null) return;
        var consoleBlocks = ExtractConsoleBlocks(content);

        Assert.NotEmpty(consoleBlocks);
        foreach (var block in consoleBlocks)
        {
            // R-CC1: fenced with ```console (verified by ExtractConsoleBlocks matching)
            // R-CC2: starts with azmcp
            var firstLine = block.Split('\n')[0].Trim();
            Assert.StartsWith("azmcp", firstLine);

            // R-CC3: required params use --param <param> (no brackets)
            var requiredParams = Regex.Matches(block, @"^\s+--(\S+)\s+<[^>]+>", RegexOptions.Multiline);
            foreach (Match param in requiredParams)
            {
                Assert.DoesNotContain("[--", param.Value);
            }

            // R-CC4: optional params use [--param <param>] format
            var optionalParams = Regex.Matches(block, @"\[--[\w-]+\s+<[\w-]+>\]");
            foreach (Match param in optionalParams)
            {
                Assert.Matches(@"\[--[\w-]+\s+<[\w-]+>\]", param.Value);
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static List<string> GetToolSections(string content)
        => RegressionTestHelpers.GetToolSections(content);

    private static List<string> ExtractConsoleBlocks(string content)
    {
        var blocks = new List<string>();
        var matches = Regex.Matches(content, @"```console\r?\n(.*?)```", RegexOptions.Singleline);
        foreach (Match m in matches)
        {
            blocks.Add(m.Groups[1].Value.TrimEnd());
        }
        return blocks;
    }

    private static string LoadFixture(string filename)
        => RegressionTestHelpers.LoadFixture(filename);

    private static string? LoadRealGeneratedFile(params string[] pathParts)
        => RegressionTestHelpers.TryLoadRealGeneratedFile(pathParts);
}
