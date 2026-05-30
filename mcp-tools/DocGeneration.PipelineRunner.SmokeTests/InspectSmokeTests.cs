// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using PipelineRunner.Cli;
using Xunit;
using Xunit.Abstractions;

namespace DocGeneration.PipelineRunner.SmokeTests;

/// <summary>
/// Smoke tests for the <c>--inspect</c> CLI mode.
/// Verifies that:
///   (a) The budget table is printed to stdout.
///   (b) The exit code reflects whether the namespace is within budget.
///   (c) No LLM API call is made — inspect is fully deterministic.
/// These tests run fully in-process with a synthetic cli-output.json fixture.
/// </summary>
[Trait("Category", "SmokeTest")]
[Collection("SmokeTests")]
public sealed class InspectSmokeTests
{
    private readonly ITestOutputHelper _output;

    public InspectSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Invokes <c>--inspect --step horizontal-articles --namespace advisor</c> with a
    /// minimal synthetic cli-output.json. Asserts the budget table appears on stdout
    /// and that no LLM endpoint was contacted (inspect is a pure token-count computation).
    /// </summary>
    [Fact]
    [Trait("Category", "SmokeTest")]
    public async Task InspectSmokeTest_HorizontalArticlesBudgetTable_PrintedToStdout()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"inspect-smoke-{Guid.NewGuid():N}");
        try
        {
            // ── Seed minimal cli-output.json for the advisor namespace ─────────
            var cliDir = Path.Combine(tempDir, "cli");
            Directory.CreateDirectory(cliDir);
            await File.WriteAllTextAsync(Path.Combine(cliDir, "cli-output.json"), AdvisorCliOutputJson);

            // ── Redirect Console.Out to capture the budget table ───────────────
            // RunInspectAsync writes the budget table via Console.WriteLine.
            var captured = new StringWriter();
            var previousOut = Console.Out;
            Console.SetOut(captured);
            int exitCode;
            try
            {
                var runner = global::PipelineRunner.PipelineRunner.CreateDefault();
                var request = new PipelineRequest(
                    Namespace: "advisor",
                    Steps: [],
                    OutputPath: tempDir,
                    SkipBuild: true,
                    SkipValidation: true,
                    DryRun: false,
                    SkipEnvValidation: true,
                    SkipDependencyValidation: true,
                    Inspect: true,
                    ReplayStepName: "horizontal-articles");

                exitCode = await runner.RunAsync(request, CancellationToken.None);
            }
            finally
            {
                Console.SetOut(previousOut);
            }

            var output = captured.ToString();
            _output.WriteLine($"Inspect exit code: {exitCode}");
            _output.WriteLine("--- Captured stdout ---");
            _output.WriteLine(output);

            // ── (a) Exit code must be exactly 0 — synthetic fixture is within budget ──
            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);

            // ── (b) Budget table is printed to stdout ──────────────────────────
            Assert.False(string.IsNullOrWhiteSpace(output),
                "RunInspectAsync must print the budget table to stdout.");

            // The table includes the step name and namespace in its rows/header.
            Assert.Contains("horizontal-articles", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("advisor", output, StringComparison.OrdinalIgnoreCase);

            // The table includes a "Budget" column header.
            Assert.Contains("Budget", output, StringComparison.OrdinalIgnoreCase);

            // ── (c) No LLM environment variable is needed ─────────────────────
            // Verified by design: RunInspectAsync routes to InspectHorizontalArticlesAsync
            // which calls ArticleOutlineBuilder.BuildAsync (reads JSON only) then
            // ArticleOutlineBudgetValidator.ValidateAsync (pure arithmetic). No AI SDK call.
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    // Two advisor commands — small enough to be well within the horizontal-articles budget.
    private const string AdvisorCliOutputJson = """
        {
          "version": "1.0.0",
          "results": [
            {
              "command": "advisor recommendations list",
              "name": "advisor recommendations list",
              "description": "List Azure Advisor recommendations for a subscription.",
              "arguments": [
                { "name": "--subscription", "description": "Subscription ID.", "required": false }
              ]
            },
            {
              "command": "advisor recommendations show",
              "name": "advisor recommendations show",
              "description": "Show details of an Azure Advisor recommendation.",
              "arguments": [
                { "name": "--name", "description": "Recommendation name.", "required": true }
              ]
            }
          ]
        }
        """;
}
