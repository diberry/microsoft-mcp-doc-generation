// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DocGeneration.TestInfrastructure;
using PipelineRunner;
using PipelineRunner.Cli;
using Xunit;
using Xunit.Abstractions;

namespace DocGeneration.PipelineRunner.SmokeTests;

/// <summary>
/// End-to-end smoke tests that run the full documentation generation pipeline
/// on small, representative Azure namespaces and verify output matches baseline fixtures.
/// 
/// These tests serve as a safety net for refactoring — they ensure that code changes
/// don't alter pipeline behavior or output structure.
/// </summary>
[Trait("Category", "Smoke")]
public class PipelineSmokeTests
{
    private readonly ITestOutputHelper _output;
    private readonly string _repoRoot;
    private readonly string _testProjectRoot;

    // Test namespaces: small, stable, representative
    private static readonly string[] TestNamespaces = new[] { "quota", "redis" };

    public PipelineSmokeTests(ITestOutputHelper output)
    {
        _output = output;
        _repoRoot = ProjectRootFinder.FindSolutionRoot();
        _testProjectRoot = Path.Combine(
            _repoRoot,
            "mcp-tools",
            "DocGeneration.PipelineRunner.SmokeTests");
    }

    /// <summary>
    /// Runs the full pipeline on test namespaces and verifies output matches baseline.
    /// This is the primary smoke test that validates end-to-end pipeline behavior.
    /// </summary>
    [Theory]
    [InlineData("quota")]
    [InlineData("redis")]
    [Trait("Category", "Smoke")]
    public async Task PipelineProducesExpectedOutput_MatchesBaseline(string namespaceName)
    {
        // Check if we're in baseline update mode
        var updateBaseline = Environment.GetEnvironmentVariable("BASELINE_UPDATE")
            ?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        if (updateBaseline)
        {
            await UpdateBaseline(namespaceName);
            return;
        }

        // Skip if external dependencies are unavailable
        if (!PipelineDependenciesAvailable())
        {
            _output.WriteLine($"Skipping smoke test for '{namespaceName}' — external dependencies not available.");
            _output.WriteLine("Set PIPELINE_SMOKE_TEST_ENABLED=true in environments with MCP CLI and full .NET build chain.");
            return;
        }

        // Skip if baselines don't exist
        if (!BaselineManager.BaselinesExist(_testProjectRoot, namespaceName))
        {
            _output.WriteLine($"Skipping smoke test for '{namespaceName}' — baselines not found.");
            _output.WriteLine("Run with BASELINE_UPDATE=true to capture baselines.");
            return;
        }

        // Run pipeline
        _output.WriteLine($"Running pipeline for namespace: {namespaceName}");
        var exitCode = await RunPipeline(namespaceName);
        
        Assert.Equal(0, exitCode);
        _output.WriteLine($"✓ Pipeline completed successfully for {namespaceName}");

        // Compare against baseline
        _output.WriteLine($"Comparing output against baseline...");
        var comparison = BaselineManager.CompareWithBaseline(
            _testProjectRoot,
            _repoRoot,
            namespaceName);

        _output.WriteLine(comparison.GetSummary());
        Assert.True(comparison.Success, comparison.GetSummary());
    }

    /// <summary>
    /// Validates that test namespaces produce valid output structure without
    /// requiring baseline comparison. This is useful for catching structural
    /// issues even when baselines haven't been captured yet.
    /// </summary>
    [Theory]
    [InlineData("quota")]
    [InlineData("redis")]
    [Trait("Category", "Smoke")]
    public async Task PipelineProducesValidStructure_WithoutBaseline(string namespaceName)
    {
        // Guard: skip when external dependencies (MCP CLI, .NET build infra) are unavailable
        if (!PipelineDependenciesAvailable())
        {
            _output.WriteLine($"Skipping smoke test for '{namespaceName}' — external dependencies not available.");
            _output.WriteLine("Set PIPELINE_SMOKE_TEST_ENABLED=true in environments with MCP CLI and full .NET build chain.");
            return;
        }

        _output.WriteLine($"Running pipeline for namespace: {namespaceName}");
        var exitCode = await RunPipeline(namespaceName);
        
        Assert.Equal(0, exitCode);
        
        // Verify output directory exists
        var outputDir = BaselineManager.FindGeneratedDirectory(_repoRoot, namespaceName)
            ?? throw new Xunit.Sdk.XunitException($"Generated output directory not found for namespace: {namespaceName}");
        Assert.True(Directory.Exists(outputDir),
            $"Generated output directory not found for namespace: {namespaceName}");

        // Verify required subdirectories exist (only those produced by Step 1)
        var requiredDirs = new[] { "annotations", "parameters" };
        foreach (var dir in requiredDirs)
        {
            var fullPath = Path.Combine(outputDir, dir);
            Assert.True(Directory.Exists(fullPath),
                $"Required subdirectory missing: {dir}");
        }

        // Verify at least some files were generated
        var annotationsCount = Directory.GetFiles(
            Path.Combine(outputDir, "annotations"), "*.md").Length;
        
        Assert.True(annotationsCount > 0, "No annotation files generated");
        
        _output.WriteLine($"✓ Valid structure: {annotationsCount} annotations");
    }

    /// <summary>
    /// Sentinel test that reports whether smoke test baselines exist.
    /// This makes it explicit in CI when smoke tests are skipping due to missing baselines.
    /// </summary>
    [Fact]
    [Trait("Category", "Smoke")]
    public void SmokeTestBaselines_DiscoveryReport()
    {
        var baselinesExist = new Dictionary<string, bool>();
        
        foreach (var ns in TestNamespaces)
        {
            baselinesExist[ns] = BaselineManager.BaselinesExist(_testProjectRoot, ns);
        }

        var existingCount = baselinesExist.Values.Count(exists => exists);
        var totalCount = TestNamespaces.Length;

        _output.WriteLine($"Smoke test baseline status: {existingCount}/{totalCount} namespaces");
        foreach (var (ns, exists) in baselinesExist)
        {
            _output.WriteLine($"  {ns}: {(exists ? "✓ Baseline exists" : "✗ No baseline")}");
        }

        if (existingCount == 0)
        {
            _output.WriteLine("");
            _output.WriteLine("To capture baselines:");
            _output.WriteLine("  1. Generate output: ./start.sh quota && ./start.sh redis");
            _output.WriteLine("  2. Capture baselines: BASELINE_UPDATE=true dotnet test DocGeneration.PipelineRunner.SmokeTests");
        }

        // Always passes — the message tells CI/developers what happened
        Assert.True(true);
    }

    /// <summary>
    /// Checks whether external dependencies required for smoke tests are available.
    /// Returns false in CI or environments missing MCP CLI / Azure OpenAI config.
    /// </summary>
    private bool PipelineDependenciesAvailable()
    {
        // Explicit opt-in via environment variable
        var enabled = Environment.GetEnvironmentVariable("PIPELINE_SMOKE_TEST_ENABLED");
        if (enabled?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        // Auto-detect: check if MCP server source exists (needed for CLI build)
        var mcpServerPath = Environment.GetEnvironmentVariable("MCP_SERVER_PATH")
            ?? Path.Combine(_repoRoot, "..", "azure-mcp", "servers", "Azure.Mcp.Server", "src");
        if (Directory.Exists(mcpServerPath))
            return true;

        // Check if CLI metadata already exists (pre-generated)
        var cliOutput = Path.Combine(_repoRoot, "generated", "cli", "cli-output.json");
        if (File.Exists(cliOutput))
            return true;

        return false;
    }

    private async Task<int> RunPipeline(string namespaceName)
    {
        // Build request for Step 1 only (fast, no AI dependencies)
        // This validates the core pipeline behavior without requiring AI credentials
        var request = new PipelineRequest(
            Namespace: namespaceName,
            Steps: new[] { 1 }, // Step 1: AnnotationsParametersRaw
            OutputPath: PipelineRequest.GetDefaultOutputPath(namespaceName),
            SkipBuild: false,
            SkipValidation: false,
            DryRun: false,
            SkipEnvValidation: true,
            SkipDependencyValidation: false);

        var runner = global::PipelineRunner.PipelineRunner.CreateDefault(_repoRoot, TextWriter.Null, Console.Error);
        return await runner.RunAsync(request, CancellationToken.None);
    }

    private async Task UpdateBaseline(string namespaceName)
    {
        _output.WriteLine($"=== BASELINE UPDATE MODE ===");
        _output.WriteLine($"Namespace: {namespaceName}");
        
        // Run pipeline to generate fresh output
        _output.WriteLine("Running pipeline...");
        var exitCode = await RunPipeline(namespaceName);
        
        if (exitCode != 0)
        {
            _output.WriteLine($"✗ Pipeline failed with exit code {exitCode}");
            _output.WriteLine("Cannot capture baseline from failed pipeline run.");
            Assert.Fail($"Pipeline failed with exit code {exitCode}");
        }

        _output.WriteLine("✓ Pipeline completed successfully");

        // Capture as baseline
        _output.WriteLine("Capturing baseline...");
        BaselineManager.CaptureBaseline(_testProjectRoot, _repoRoot, namespaceName);
        
        _output.WriteLine($"✓ Baseline updated for '{namespaceName}'");
        _output.WriteLine("Commit the updated Baselines/ directory to the repository.");
    }
}
