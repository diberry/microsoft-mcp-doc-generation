// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using DocGeneration.TestInfrastructure;
using PipelineRunner;
using PipelineRunner.Cli;
using Xunit;
using Xunit.Abstractions;

namespace DocGeneration.PipelineRunner.SmokeTests;

/// <summary>
/// Smoke tests for <c>--inspect</c> mode (Point 13, PRD-QUALITY-2026-05-30 Item D).
/// These tests verify budget-table output, JSON file writing, and exit-code behaviour
/// without invoking the LLM. They use synthetic fixture data so no external
/// dependencies are required.
/// </summary>
[Trait("Category", "Smoke")]
public sealed class InspectSmokeTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _repoRoot;
    private readonly string _testTempRoot;

    public InspectSmokeTests(ITestOutputHelper output)
    {
        _output = output;
        _repoRoot = ProjectRootFinder.FindSolutionRoot();
        _testTempRoot = Path.Combine(
            AppContext.BaseDirectory,
            "inspect-smoke-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testTempRoot))
        {
            Directory.Delete(_testTempRoot, recursive: true);
        }
    }

    /// <summary>
    /// When <c>--output</c> is explicitly provided in inspect mode,
    /// <c>inspect-budget.json</c> must be written to the output directory
    /// with the expected top-level JSON structure.
    /// </summary>
    [Fact]
    public async Task Inspect_WithExplicitOutput_WritesInspectBudgetJson()
    {
        // Arrange: create a synthetic cli-output.json for the horizontal-articles builder
        await WriteMinimalCliOutputAsync(_testTempRoot, "quota");

        var request = new PipelineRequest(
            Namespace: "quota",
            Steps: PipelineRequest.DefaultSteps,
            OutputPath: _testTempRoot,
            SkipBuild: true,
            SkipValidation: true,
            DryRun: false,
            SkipEnvValidation: true,
            SkipDependencyValidation: true,
            Inspect: true,
            InspectShow: "prompt-budget",
            ReplayStepName: "horizontal-articles",
            WriteJsonOutput: true);

        var runner = global::PipelineRunner.PipelineRunner.CreateDefault(_repoRoot, TextWriter.Null, TextWriter.Null);

        // Act
        var exitCode = await runner.RunAsync(request, CancellationToken.None);
        _output.WriteLine($"Exit code: {exitCode}");

        // Assert — inspect-budget.json must exist and parse successfully
        var jsonPath = Path.Combine(_testTempRoot, "inspect-budget.json");
        Assert.True(File.Exists(jsonPath), $"inspect-budget.json was not written to: {_testTempRoot}");

        var json = await File.ReadAllTextAsync(jsonPath);
        _output.WriteLine($"inspect-budget.json content:\n{json}");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("model", out _), "JSON must contain 'model' field");
        Assert.True(root.TryGetProperty("rows", out var rowsElement), "JSON must contain 'rows' array");
        Assert.Equal(JsonValueKind.Array, rowsElement.ValueKind);

        // At minimum we expect a row for the 'quota' namespace
        Assert.True(rowsElement.GetArrayLength() > 0, "rows array must not be empty");

        var firstRow = rowsElement[0];
        Assert.True(firstRow.TryGetProperty("step", out _), "Each row must have 'step'");
        Assert.True(firstRow.TryGetProperty("namespace", out _), "Each row must have 'namespace'");
        Assert.True(firstRow.TryGetProperty("estimatedTokens", out _), "Each row must have 'estimatedTokens'");
        Assert.True(firstRow.TryGetProperty("budget", out _), "Each row must have 'budget'");
        Assert.True(firstRow.TryGetProperty("headroom", out _), "Each row must have 'headroom'");
        Assert.True(firstRow.TryGetProperty("topItems", out _), "Each row must have 'topItems'");
    }

    /// <summary>
    /// When <c>--output</c> is NOT explicitly provided in inspect mode,
    /// no <c>inspect-budget.json</c> file must be written.
    /// </summary>
    [Fact]
    public async Task Inspect_WithoutExplicitOutput_DoesNotWriteInspectBudgetJson()
    {
        await WriteMinimalCliOutputAsync(_testTempRoot, "quota");

        // WriteJsonOutput = false  →  stdout only
        var request = new PipelineRequest(
            Namespace: "quota",
            Steps: PipelineRequest.DefaultSteps,
            OutputPath: _testTempRoot,
            SkipBuild: true,
            SkipValidation: true,
            DryRun: false,
            SkipEnvValidation: true,
            SkipDependencyValidation: true,
            Inspect: true,
            InspectShow: "prompt-budget",
            ReplayStepName: "horizontal-articles",
            WriteJsonOutput: false);

        var runner = global::PipelineRunner.PipelineRunner.CreateDefault(_repoRoot, TextWriter.Null, TextWriter.Null);
        await runner.RunAsync(request, CancellationToken.None);

        var jsonPath = Path.Combine(_testTempRoot, "inspect-budget.json");
        Assert.False(File.Exists(jsonPath), "inspect-budget.json must NOT be written when --output is omitted");
    }

    /// <summary>
    /// When the estimated token count exceeds the budget, <c>RunAsync</c>
    /// must return <see cref="global::PipelineRunner.PipelineRunner.FatalExitCode"/> (1).
    /// </summary>
    [Fact]
    public async Task Inspect_OverBudget_ReturnsFatalExitCode()
    {
        // Write a synthetic cli-output.json whose evidence content is large enough
        // to exceed the 150k-token horizontal-articles budget (4 chars ≈ 1 token →
        // 600_001 chars  →  150,001 tokens).
        await WriteOversizedCliOutputAsync(_testTempRoot, "quota");

        var request = new PipelineRequest(
            Namespace: "quota",
            Steps: PipelineRequest.DefaultSteps,
            OutputPath: _testTempRoot,
            SkipBuild: true,
            SkipValidation: true,
            DryRun: false,
            SkipEnvValidation: true,
            SkipDependencyValidation: true,
            Inspect: true,
            InspectShow: "prompt-budget",
            ReplayStepName: "horizontal-articles",
            WriteJsonOutput: false);

        var runner = global::PipelineRunner.PipelineRunner.CreateDefault(_repoRoot, TextWriter.Null, TextWriter.Null);
        var exitCode = await runner.RunAsync(request, CancellationToken.None);

        _output.WriteLine($"Exit code: {exitCode}");
        Assert.Equal(global::PipelineRunner.PipelineRunner.FatalExitCode, exitCode);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Writes a minimal cli-output.json for the given namespace.</summary>
    private static async Task WriteMinimalCliOutputAsync(string root, string ns)
    {
        var cliDir = Path.Combine(root, "cli");
        Directory.CreateDirectory(cliDir);

        var payload = new
        {
            results = new[]
            {
                new
                {
                    command   = $"{ns} list",
                    name      = $"{ns} list",
                    description = $"List {ns} resources.",
                    option    = Array.Empty<object>(),
                    metadata  = new { destructive = new { value = false }, readOnly = new { value = true }, secret = new { value = false } }
                }
            }
        };

        await File.WriteAllTextAsync(
            Path.Combine(cliDir, "cli-output.json"),
            JsonSerializer.Serialize(payload));
    }

    /// <summary>
    /// Writes a cli-output.json whose combined evidence exceeds the 150 k-token
    /// horizontal-articles budget (150,001 tokens → 600,004 chars at 4 chars/token).
    /// </summary>
    private static async Task WriteOversizedCliOutputAsync(string root, string ns)
    {
        var cliDir = Path.Combine(root, "cli");
        Directory.CreateDirectory(cliDir);

        // One tool with a description large enough to push the total over budget.
        var bigDescription = new string('x', 600_010);

        var payload = new
        {
            results = new[]
            {
                new
                {
                    command     = $"{ns} list",
                    name        = $"{ns} list",
                    description = bigDescription,
                    option      = Array.Empty<object>(),
                    metadata    = new { destructive = new { value = false }, readOnly = new { value = true }, secret = new { value = false } }
                }
            }
        };

        await File.WriteAllTextAsync(
            Path.Combine(cliDir, "cli-output.json"),
            JsonSerializer.Serialize(payload));
    }
}
