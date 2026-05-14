// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;
using CSharpGenerator.Generators;
using Shared;
using Xunit;
using Xunit.Abstractions;

namespace PipelineRunner.Tests.Integration;

/// <summary>
/// End-to-end integration tests that exercise the full CLI tab content pipeline
/// on real namespace data (storage, compute, appservice, azurebackup, functions).
/// </summary>
public class CliTabPilotTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private FileNameContext _nameContext = null!;
    private string _templateFile = null!;
    private string _repoRoot = null!;

    private static readonly string[] PilotNamespaces =
        ["storage", "compute", "appservice", "azurebackup", "functions"];

    public CliTabPilotTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        _repoRoot = FindRepoRoot();
        _templateFile = Path.Combine(_repoRoot, "mcp-tools", "templates", "cli-parameter-template.hbs");
        Assert.True(File.Exists(_templateFile), $"Template file not found: {_templateFile}");

        _nameContext = await FileNameContext.CreateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("storage")]
    [InlineData("compute")]
    [InlineData("appservice")]
    [InlineData("azurebackup")]
    [InlineData("functions")]
    public async Task FullPipeline_ProducesValidCliTabs_ForNamespace(string ns)
    {
        // Arrange: load real CLI JSON
        var cliJsonPath = Path.Combine(_repoRoot, $"generated-{ns}", "cli", "cli-output.json");
        if (!File.Exists(cliJsonPath))
        {
            _output.WriteLine($"SKIP: {cliJsonPath} not found");
            return;
        }

        var json = await File.ReadAllTextAsync(cliJsonPath);
        var outputDir = Path.Combine(Path.GetTempPath(), $"cli-pilot-test-{ns}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            // Step 1: CliJsonMapper
            var cliTools = CliJsonMapper.MapFromCliOutput(json);
            Assert.NotEmpty(cliTools);
            _output.WriteLine($"[{ns}] Mapped {cliTools.Count} CLI tools");

            // Step 2: CliParameterGenerator
            var paramDir = Path.Combine(outputDir, "parameter-cli");
            await CliParameterGenerator.GenerateParameterCliFilesAsync(
                cliTools, _templateFile, paramDir, _nameContext,
                "1.0.0-pilot", DateTime.UtcNow);

            var paramFiles = Directory.GetFiles(paramDir, "*.md");
            Assert.NotEmpty(paramFiles);
            _output.WriteLine($"[{ns}] Generated {paramFiles.Length} parameter-cli files");

            // Step 3: CliExampleCommandGenerator
            var exampleDir = Path.Combine(outputDir, "example-commands");
            await CliExampleCommandGenerator.GenerateExampleCommandFilesAsync(
                cliTools, exampleDir, _nameContext,
                "1.0.0-pilot", DateTime.UtcNow);

            var exampleFiles = Directory.GetFiles(exampleDir, "*.md");
            Assert.NotEmpty(exampleFiles);
            _output.WriteLine($"[{ns}] Generated {exampleFiles.Length} example-commands files");

            // Step 4: CliContentAssembler
            var assembledContent = await CliContentAssembler.AssembleAllCliContentAsync(
                cliTools, paramDir, exampleDir, _nameContext);
            Assert.NotEmpty(assembledContent);
            _output.WriteLine($"[{ns}] Assembled CLI content for {assembledContent.Count} tools");

            // Step 5: CliTabWrapper — wrap each assembled block
            int tabWrappedCount = 0;
            var validationErrors = new List<string>();

            foreach (var (command, cliContent) in assembledContent)
            {
                var fakeMcpContent = $"This tool executes `{command}` via MCP Server.\n\nSee parameters below.";
                var wrapped = CliTabWrapper.WrapWithTabs(fakeMcpContent, cliContent);

                // Validate tab structure
                ValidateTabStructure(wrapped, command, validationErrors);
                ValidateNoBrokenPipes(wrapped, command, validationErrors);
                ValidateMarkdownIntegrity(wrapped, command, validationErrors);

                tabWrappedCount++;
            }

            Assert.Empty(validationErrors);
            _output.WriteLine($"[{ns}] Tab-wrapped {tabWrappedCount} tools — all valid");

            // Coverage report
            _output.WriteLine($"\n--- COVERAGE REPORT [{ns}] ---");
            _output.WriteLine($"  Total CLI tools parsed: {cliTools.Count}");
            _output.WriteLine($"  Parameter files generated: {paramFiles.Length}");
            _output.WriteLine($"  Example command files generated: {exampleFiles.Length}");
            _output.WriteLine($"  CLI content assembled: {assembledContent.Count}");
            _output.WriteLine($"  Tab-wrapped successfully: {tabWrappedCount}");
            _output.WriteLine($"  Coverage: {tabWrappedCount * 100 / cliTools.Count}%");
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task CoverageReport_AcrossAllPilotNamespaces()
    {
        var report = new StringBuilder();
        report.AppendLine("=== CLI TAB PILOT COVERAGE REPORT ===");
        report.AppendLine();

        int totalTools = 0;
        int totalTabbed = 0;

        foreach (var ns in PilotNamespaces)
        {
            var cliJsonPath = Path.Combine(_repoRoot, $"generated-{ns}", "cli", "cli-output.json");
            if (!File.Exists(cliJsonPath))
            {
                report.AppendLine($"  [{ns}] SKIPPED — no cli-output.json");
                continue;
            }

            var json = await File.ReadAllTextAsync(cliJsonPath);
            var cliTools = CliJsonMapper.MapFromCliOutput(json);

            var outputDir = Path.Combine(Path.GetTempPath(), $"cli-coverage-{ns}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(outputDir);

            try
            {
                var paramDir = Path.Combine(outputDir, "parameter-cli");
                await CliParameterGenerator.GenerateParameterCliFilesAsync(
                    cliTools, _templateFile, paramDir, _nameContext,
                    "1.0.0-pilot", DateTime.UtcNow);

                var exampleDir = Path.Combine(outputDir, "example-commands");
                await CliExampleCommandGenerator.GenerateExampleCommandFilesAsync(
                    cliTools, exampleDir, _nameContext,
                    "1.0.0-pilot", DateTime.UtcNow);

                var assembled = await CliContentAssembler.AssembleAllCliContentAsync(
                    cliTools, paramDir, exampleDir, _nameContext);

                totalTools += cliTools.Count;
                totalTabbed += assembled.Count;

                report.AppendLine($"  [{ns}] {assembled.Count}/{cliTools.Count} tools got CLI tabs ({assembled.Count * 100 / Math.Max(cliTools.Count, 1)}%)");
            }
            finally
            {
                if (Directory.Exists(outputDir))
                    Directory.Delete(outputDir, recursive: true);
            }
        }

        report.AppendLine();
        report.AppendLine($"  TOTAL: {totalTabbed}/{totalTools} tools got CLI tabs ({(totalTools > 0 ? totalTabbed * 100 / totalTools : 0)}%)");
        report.AppendLine("=====================================");

        _output.WriteLine(report.ToString());

        // Only assert coverage when data files exist (graceful skip in CI where files are missing)
        if (totalTools > 0)
        {
            Assert.True(totalTabbed > 0, "Expected at least some tools to get CLI tab content");
        }
    }

    #region Validation helpers

    private static void ValidateTabStructure(string wrapped, string command, List<string> errors)
    {
        if (!wrapped.Contains("#### [MCP Server](#tab/mcp-server)"))
            errors.Add($"[{command}] Missing MCP Server tab header");

        if (!wrapped.Contains("#### [Azure MCP CLI](#tab/azure-mcp-cli)"))
            errors.Add($"[{command}] Missing CLI tab header");

        if (!wrapped.Contains("---"))
            errors.Add($"[{command}] Missing tab group terminator (---)");

        // MCP tab must come before CLI tab
        var mcpIdx = wrapped.IndexOf("#### [MCP Server]", StringComparison.Ordinal);
        var cliIdx = wrapped.IndexOf("#### [Azure MCP CLI]", StringComparison.Ordinal);
        if (mcpIdx >= 0 && cliIdx >= 0 && mcpIdx > cliIdx)
            errors.Add($"[{command}] MCP Server tab must appear before CLI tab");
    }

    private static void ValidateNoBrokenPipes(string content, string command, List<string> errors)
    {
        // Check that pipe chars inside table cells are escaped
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            if (!line.TrimStart().StartsWith('|'))
                continue;

            // Skip separator lines
            if (Regex.IsMatch(line, @"^\s*\|[-\s|]+\|\s*$"))
                continue;

            // Count unescaped pipes — for a valid table row, cell contents must not have bare pipes
            var cells = SplitTableRow(line);
            if (cells is null)
                continue;

            foreach (var cell in cells)
            {
                // A bare pipe inside a cell (not escaped) would break the table
                if (cell.Contains('|') && !cell.Contains("\\|"))
                    errors.Add($"[{command}] Unescaped pipe in table cell: '{cell.Trim()}'");
            }
        }
    }

    private static void ValidateMarkdownIntegrity(string content, string command, List<string> errors)
    {
        // Ensure code fences are balanced
        var fenceCount = Regex.Matches(content, @"^```", RegexOptions.Multiline).Count;
        if (fenceCount % 2 != 0)
            errors.Add($"[{command}] Unbalanced code fences (found {fenceCount})");

        // Ensure no empty table rows (| | | | |)
        if (Regex.IsMatch(content, @"^\|\s*\|\s*\|\s*\|\s*\|", RegexOptions.Multiline))
            errors.Add($"[{command}] Empty table row detected");
    }

    /// <summary>
    /// Splits a markdown table row into cells, respecting escaped pipes.
    /// Returns null if the line doesn't look like a table row.
    /// </summary>
    private static string[]? SplitTableRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 2 || !trimmed.StartsWith('|') || !trimmed.EndsWith('|'))
            return null;

        // Remove leading/trailing pipe, then split on unescaped pipes
        var inner = trimmed.Substring(1, trimmed.Length - 2);
        if (inner.Length == 0)
            return null;

        // Replace escaped pipes with placeholder, split, restore
        var placeholder = "\x01";
        var safe = inner.Replace("\\|", placeholder);
        var cells = safe.Split('|');
        return cells.Select(c => c.Replace(placeholder, "\\|")).ToArray();
    }

    #endregion

    #region Helpers

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
            if (File.Exists(Path.Combine(dir, "mcp-doc-generation.sln")))
                return dir;
        }

        // Fallback: try known location
        var fallback = @"C:\Users\diberry\microsoft-mcp-doc-generation";
        if (Directory.Exists(fallback))
            return fallback;

        throw new InvalidOperationException("Could not find repo root (mcp-doc-generation.sln)");
    }

    #endregion
}
