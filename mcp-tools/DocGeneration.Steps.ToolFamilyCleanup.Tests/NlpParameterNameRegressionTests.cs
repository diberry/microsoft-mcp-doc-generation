// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Regression tests for MCP tab parameters (R-MP1 through R-MP4) and
/// CLI tab parameters (R-CP1 through R-CP4).
/// Validates NLP display names in MCP tab, CLI switch format in CLI tab.
/// </summary>
public class NlpParameterNameRegressionTests
{
    private static readonly string FixturesDir = GetFixturesDir();
    private static readonly string RepoRoot = FindRepoRoot();

    // ── R-MP1: MCP table headers ─────────────────────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_MP1_McpTableHeaders()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var mcpTables = ExtractMcpParameterTables(content);

        Assert.NotEmpty(mcpTables);
        foreach (var table in mcpTables)
        {
            var headerLine = table.Split('\n')[0].Trim();
            Assert.Contains("Parameter", headerLine);
            Assert.Contains("Required or optional", headerLine);
            Assert.Contains("Description", headerLine);
        }
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_MP1_McpTableHeaders_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        var mcpTables = ExtractMcpParameterTables(content);

        Assert.NotEmpty(mcpTables);
        foreach (var table in mcpTables)
        {
            var headerLine = table.Split('\n')[0].Trim();
            Assert.Contains("Parameter", headerLine);
            Assert.Contains("Required or optional", headerLine);
            Assert.Contains("Description", headerLine);
        }
    }

    // ── R-MP2: MCP param names are bolded NLP names ──────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_MP2_McpParamNamesAreBoldedNlp()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var mcpTables = ExtractMcpParameterTables(content);

        foreach (var table in mcpTables)
        {
            var dataRows = GetTableDataRows(table);
            foreach (var row in dataRows)
            {
                var paramCell = GetCellValue(row, 0);
                // Must be bolded: **Name**
                Assert.Matches(@"^\*\*.+\*\*$", paramCell.Trim());
            }
        }
    }

    // ── R-MP3: MCP param names do NOT contain -- prefix ──────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_MP3_McpParamNamesNoCliSwitchFormat()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var mcpTables = ExtractMcpParameterTables(content);

        foreach (var table in mcpTables)
        {
            var dataRows = GetTableDataRows(table);
            foreach (var row in dataRows)
            {
                var paramCell = GetCellValue(row, 0);
                Assert.DoesNotContain("--", paramCell);
                Assert.DoesNotContain("`", paramCell);
            }
        }
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_MP3_McpParamNamesNoCliSwitchFormat_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        var mcpTables = ExtractMcpParameterTables(content);

        foreach (var table in mcpTables)
        {
            var dataRows = GetTableDataRows(table);
            foreach (var row in dataRows)
            {
                var paramCell = GetCellValue(row, 0);
                Assert.DoesNotContain("--", paramCell);
            }
        }
    }

    // ── R-MP4: Required or optional column values ────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_MP4_McpRequiredColumnValues()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var mcpTables = ExtractMcpParameterTables(content);

        foreach (var table in mcpTables)
        {
            var dataRows = GetTableDataRows(table);
            foreach (var row in dataRows)
            {
                var reqCell = GetCellValue(row, 1).Trim();
                Assert.True(reqCell == "Required" || reqCell == "Optional",
                    $"MCP 'Required or optional' column must be exactly 'Required' or 'Optional', got: '{reqCell}'");
            }
        }
    }

    // ── R-CP1: CLI table headers ─────────────────────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_CP1_CliTableHeaders()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var cliTables = ExtractCliParameterTables(content);

        Assert.NotEmpty(cliTables);
        foreach (var table in cliTables)
        {
            var headerLine = table.Split('\n')[0].Trim();
            Assert.Contains("Parameter", headerLine);
            Assert.Contains("Type", headerLine);
            Assert.Contains("Required", headerLine);
            Assert.Contains("Description", headerLine);
        }
    }

    // ── R-CP2: CLI param names use backtick-wrapped --switch format ──────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_CP2_CliParamNamesAreSwitchFormat()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var cliTables = ExtractCliParameterTables(content);

        foreach (var table in cliTables)
        {
            var dataRows = GetTableDataRows(table);
            foreach (var row in dataRows)
            {
                var paramCell = GetCellValue(row, 0).Trim();
                // Must be backtick-wrapped --switch: `--name`
                Assert.Matches(@"^`--[\w-]+`$", paramCell);
            }
        }
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_CP2_CliParamNamesAreSwitchFormat_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        var cliTables = ExtractCliParameterTables(content);

        Assert.NotEmpty(cliTables);
        foreach (var table in cliTables)
        {
            var dataRows = GetTableDataRows(table);
            foreach (var row in dataRows)
            {
                var paramCell = GetCellValue(row, 0).Trim();
                Assert.Matches(@"^`--[\w-]+`$", paramCell);
            }
        }
    }

    // ── R-CP3: Required column values (Yes/No) ──────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_CP3_CliRequiredColumnValues()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var cliTables = ExtractCliParameterTables(content);

        foreach (var table in cliTables)
        {
            var dataRows = GetTableDataRows(table);
            foreach (var row in dataRows)
            {
                var reqCell = GetCellValue(row, 2).Trim();
                Assert.True(reqCell == "Yes" || reqCell == "No",
                    $"CLI 'Required' column must be exactly 'Yes' or 'No', got: '{reqCell}'");
            }
        }
    }

    // ── R-CP4: Type column contains valid types ──────────────────────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_CP4_CliTypeColumnValid()
    {
        var content = LoadFixture("ValidToolFamily.md");
        var cliTables = ExtractCliParameterTables(content);
        var validTypes = new HashSet<string> { "string", "integer", "boolean", "number", "array", "object" };

        foreach (var table in cliTables)
        {
            var dataRows = GetTableDataRows(table);
            foreach (var row in dataRows)
            {
                var typeCell = GetCellValue(row, 1).Trim();
                Assert.True(validTypes.Contains(typeCell),
                    $"CLI 'Type' column must be a valid type, got: '{typeCell}'");
            }
        }
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_CP4_CliTypeColumnValid_RealFile()
    {
        var content = LoadRealGeneratedFile("generated-azurebackup", "tool-family", "azure-backup.md");
        var cliTables = ExtractCliParameterTables(content);
        var validTypes = new HashSet<string> { "string", "integer", "boolean", "number", "array", "object" };

        foreach (var table in cliTables)
        {
            var dataRows = GetTableDataRows(table);
            foreach (var row in dataRows)
            {
                var typeCell = GetCellValue(row, 1).Trim();
                Assert.True(validTypes.Contains(typeCell),
                    $"CLI 'Type' column must be a valid type, got: '{typeCell}'");
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts parameter tables from MCP tab sections.
    /// MCP tables have header: Parameter | Required or optional | Description
    /// </summary>
    private static List<string> ExtractMcpParameterTables(string content)
    {
        var tables = new List<string>();
        var toolSections = GetToolSections(content);

        foreach (var section in toolSections)
        {
            var mcpStart = section.IndexOf("#### [MCP Server](#tab/mcp-server)", StringComparison.Ordinal);
            var cliStart = section.IndexOf("#### [Azure MCP CLI](#tab/azure-mcp-cli)", StringComparison.Ordinal);
            if (mcpStart < 0 || cliStart < 0) continue;

            var mcpContent = section[mcpStart..cliStart];
            var tableMatch = Regex.Match(mcpContent, @"(\| Parameter .+Required or optional.+\r?\n\|[-| ]+\r?\n(?:\|.+\r?\n)+)", RegexOptions.Multiline);
            if (tableMatch.Success)
                tables.Add(tableMatch.Value.TrimEnd());
        }
        return tables;
    }

    /// <summary>
    /// Extracts parameter tables from CLI tab sections.
    /// CLI tables have header: Parameter | Type | Required | Description
    /// </summary>
    private static List<string> ExtractCliParameterTables(string content)
    {
        var tables = new List<string>();
        var toolSections = GetToolSections(content);

        foreach (var section in toolSections)
        {
            var cliStart = section.IndexOf("#### [Azure MCP CLI](#tab/azure-mcp-cli)", StringComparison.Ordinal);
            if (cliStart < 0) continue;

            var cliContent = section[cliStart..];
            var tableMatch = Regex.Match(cliContent, @"(\| Parameter \| Type \| Required \| Description \|\r?\n\|[-| ]+\r?\n(?:\|.+\r?\n)+)", RegexOptions.Multiline);
            if (tableMatch.Success)
                tables.Add(tableMatch.Value.TrimEnd());
        }
        return tables;
    }

    private static List<string> GetTableDataRows(string table)
    {
        var lines = table.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.StartsWith("|"))
            .ToList();
        // Skip header row (index 0) and separator row (index 1)
        return lines.Skip(2).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
    }

    private static string GetCellValue(string row, int cellIndex)
    {
        var cells = row.Split('|')
            .Where(c => !string.IsNullOrEmpty(c.Trim()))
            .ToArray();
        return cellIndex < cells.Length ? cells[cellIndex].Trim() : "";
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
