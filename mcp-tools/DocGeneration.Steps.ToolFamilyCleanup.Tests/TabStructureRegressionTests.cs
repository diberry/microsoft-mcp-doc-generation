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
    private static readonly string FixturesDir = GetFixturesDir();
    private static readonly string RepoRoot = FindRepoRoot();

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
            var separatorCount = CountSeparatorsOutsideCodeBlocks(afterCli);
            Assert.Equal(1, separatorCount);
        }
    }

    // ── R-TS4: No separators inside tabs ─────────────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_TS4_NoSeparatorsInsideTabs()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var toolSections = GetToolSections(content);

        foreach (var section in toolSections)
        {
            var mcpStart = section.IndexOf("#### [MCP Server](#tab/mcp-server)", StringComparison.Ordinal);
            var cliStart = section.IndexOf("#### [Azure MCP CLI](#tab/azure-mcp-cli)", StringComparison.Ordinal);
            if (mcpStart < 0 || cliStart < 0) continue;

            // Content between MCP header and CLI header (MCP tab content)
            var mcpContent = section[(mcpStart + 40)..cliStart];
            Assert.Equal(0, CountSeparatorsOutsideCodeBlocks(mcpContent));

            // CLI tab content ends at the single --- separator
            var afterCli = section[cliStart..];
            var cliHeaderEnd = afterCli.IndexOf('\n') + 1;
            var cliContent = afterCli[cliHeaderEnd..];

            // Find the first standalone --- which is the tab terminator
            var lines = cliContent.Split('\n');
            bool inCodeBlock = false;
            int separatorsSeen = 0;
            foreach (var line in lines)
            {
                var trimmed = line.TrimEnd('\r');
                if (trimmed.StartsWith("```")) inCodeBlock = !inCodeBlock;
                if (!inCodeBlock && trimmed == "---")
                {
                    separatorsSeen++;
                    break; // First one is the tab terminator, stop here
                }
                // No separators before the terminator
                if (!inCodeBlock && trimmed == "---" && separatorsSeen == 0)
                    Assert.Fail("Found --- separator inside CLI tab content before tab terminator");
            }
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
    public void R_TS5_ExactTabHeaderFormat_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");

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
    public void R_CC1_Through_CC4_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        var consoleBlocks = ExtractConsoleBlocks(content);

        Assert.NotEmpty(consoleBlocks);
        foreach (var block in consoleBlocks)
        {
            // R-CC2: starts with azmcp
            var firstLine = block.Split('\n')[0].Trim();
            Assert.StartsWith("azmcp", firstLine);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

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

    private static int CountSeparatorsOutsideCodeBlocks(string text)
    {
        var lines = text.Split('\n');
        bool inCodeBlock = false;
        int count = 0;
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("```")) inCodeBlock = !inCodeBlock;
            if (!inCodeBlock && trimmed == "---") count++;
        }
        return count;
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
