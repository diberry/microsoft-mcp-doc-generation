// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using Shared;
using ToolGeneration_Improved.Models;
using Xunit;
using Xunit.Abstractions;

namespace DocGeneration.PipelineRunner.SmokeTests;

/// <summary>
/// Smoke tests for the <c>--replay</c> CLI mode.
/// Verifies that replaying a single step writes artifacts only for that step,
/// leaving all other step directories untouched (step isolation).
/// These tests run fully in-process with synthetic fixtures — no live LLM or MCP CLI required.
/// </summary>
[Trait("Category", "SmokeTest")]
[Collection("SmokeTests")]
public sealed class ReplaySmokeTests
{
    private readonly ITestOutputHelper _output;

    // Step identifier slugs (deterministic, match GetStepIdentifierSlug output).
    private const string Step1Slug = "step-1-generate-annotations-parameters-and-raw-tools";
    private const string Step2Slug = "step-2-generate-example-prompts";
    private const string Step3Slug = "step-3-compose-and-improve-tool-files";

    public ReplaySmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Replays the <c>tool-generation</c> step against a synthetic advisor run directory.
    /// Asserts that:
    ///   (a) Only the tool-generation step directory is written during replay.
    ///   (b) No steps beyond the target step (steps 4+) write artifacts.
    ///   (c) The RecordingProcessRunner records exactly the composition sub-process
    ///       and no other step-level processes.
    /// The step itself may fail gracefully (AI credentials unavailable in test env);
    /// the isolation guarantee holds regardless of the step's success or failure.
    /// </summary>
    [Fact]
    [Trait("Category", "SmokeTest")]
    public async Task ReplaySmokeTest_OnlyTargetStepWritesArtifacts()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"replay-smoke-{Guid.NewGuid():N}");
        const string runId = "advisor-replay-run-smoke";
        var runDir = Path.Combine(testRoot, "runs", runId);

        try
        {
            // ── Scaffold minimal repo-root layout ──────────────────────────────
            Directory.CreateDirectory(Path.Combine(testRoot, "mcp-tools", "scripts"));
            File.WriteAllText(Path.Combine(testRoot, "mcp-doc-generation.sln"), string.Empty);
            Directory.CreateDirectory(runDir);

            // ── Seed advisor CLI metadata in the run directory ─────────────────
            var advisorCommands = new[]
            {
                "advisor recommendations list",
                "advisor recommendations show",
            };

            await SeedCliMetadataAsync(runDir, advisorCommands);

            // ── Seed upstream step-result.json for step 1 and step 2 ───────────
            // ToolGenerationStep (step 3) depends on steps 1 and 2.
            // TryValidateReplayUpstreamArtifacts checks for step-result.json
            // in each dependency's slug directory.
            SeedUpstreamStepResult(runDir, Step1Slug, "Generate annotations, parameters, and raw tools");
            SeedUpstreamStepResult(runDir, Step2Slug, "Generate example prompts");

            // ── Seed tool artifact prerequisite files ─────────────────────────
            // GetPrerequisiteIssues checks that raw-tool, annotation, parameter,
            // and example-prompts files exist before running composition.
            // GetComposedOutputIssues checks that tools-composed files exist after composition.
            await SeedToolArtifactFilesAsync(runDir, advisorCommands);

            // ── Build runner with stubs ────────────────────────────────────────
            var processRunner = new RecordingProcessRunner();
            var stepRegistry = StepRegistry.CreateDefault(Path.Combine(testRoot, "mcp-tools", "scripts"));
            var contextFactory = new PipelineContextFactory(
                processRunner,
                new WorkspaceManager(),
                new CliMetadataLoader(),
                new TargetMatcher(),
                new SmokeStubFilteredCliWriter(),
                new SmokeStubBuildCoordinator(),
                new SmokeStubAiCapabilityProbe(),
                new ConsoleReportWriter(TextWriter.Null, TextWriter.Null),
                testRoot,
                ConfigureSmokeContext);

            var runner = new global::PipelineRunner.PipelineRunner(
                stepRegistry,
                contextFactory,
                brandMappingLoader: new SmokeStubBrandMappingLoader());

            var request = new PipelineRequest(
                Namespace: "advisor",
                Steps: [],
                OutputPath: Path.Combine(testRoot, "generated-advisor"),
                SkipBuild: true,
                SkipValidation: true,
                DryRun: false,
                SkipEnvValidation: true,
                SkipDependencyValidation: true,
                Replay: true,
                ReplayFromRunId: runId,
                ReplayStepName: "tool-generation");

            // ── Execute replay ─────────────────────────────────────────────────
            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            _output.WriteLine($"Replay exit code: {exitCode}");
            _output.WriteLine($"Process invocations: {processRunner.Invocations.Count}");
            foreach (var inv in processRunner.Invocations)
                _output.WriteLine($"  {inv.DisplayCommand}");

            // ── (a) Exit code must be exactly 0 — RecordingProcessRunner returns 0 ──
            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);

            // ── (b) Step isolation: step-3 directory must exist ───────────────
            var step3Dir = Path.Combine(runDir, Step3Slug);
            Assert.True(Directory.Exists(step3Dir),
                $"Step 3 output directory '{Step3Slug}' must be created during replay.");
            Assert.True(File.Exists(Path.Combine(step3Dir, StepResultWriter.FileName)),
                "Step 3 must write step-result.json.");

            // ── (c) Isolation: steps 4+ must NOT have written anything ─────────
            // Only assert that later steps did not run — other directories (e.g.,
            // "tools", "critical-failures") may be legitimately created by step 3.
            Assert.Empty(Directory.GetDirectories(runDir, "step-4-*"));
            Assert.Empty(Directory.GetDirectories(runDir, "step-5-*"));
            Assert.Empty(Directory.GetDirectories(runDir, "step-6-*"));

            // ── (d) RecordingProcessRunner: only composition was invoked ───────
            // Exactly one dotnet run invocation for the composition project.
            Assert.Single(processRunner.Invocations);
            var invocationArgs = string.Join(" ", processRunner.Invocations[0].Arguments);
            Assert.Contains("ToolGeneration.Composition", invocationArgs, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void ConfigureSmokeContext(PipelineContext context)
    {
        context.Items["ToolGenerationStep.ToolImproverOverride"] =
            static (ToolGenerationContext toolContext, CancellationToken _) => Task.FromResult(new ImprovedToolData
            {
                FileName = toolContext.ToolName,
                OriginalContent = toolContext.ComposedContent,
                ImprovedContent = toolContext.ComposedContent,
                WasImproved = false
            });
    }

    private static async Task SeedCliMetadataAsync(string runDir, string[] commands)
    {
        var cliDir = Path.Combine(runDir, "cli");
        Directory.CreateDirectory(cliDir);

        // cli-output.json
        var toolsJson = string.Join(",\n    ", commands.Select(cmd =>
            $$"""{ "command": "{{cmd}}", "name": "{{cmd}}", "description": "Smoke test stub for {{cmd}}" }"""));
        var cliOutputJson = $$"""
            {
              "version": "1.0.0",
              "results": [
                {{toolsJson}}
              ]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(cliDir, "cli-output.json"), cliOutputJson);

        // cli-namespace.json — required by LoadNamespacesAsync
        var namespaceJson = """
            {
              "results": [
                { "name": "advisor" }
              ]
            }
            """;
        await File.WriteAllTextAsync(Path.Combine(cliDir, "cli-namespace.json"), namespaceJson);
    }

    private static void SeedUpstreamStepResult(string runDir, string stepSlug, string stepName)
    {
        var stepDir = Path.Combine(runDir, stepSlug);
        StepResultWriter.Write(stepDir, new StepResultFile
        {
            Version = 1,
            Status = StepResultStatus.Success,
            Step = stepName,
            Namespace = "advisor",
            OutputFileCount = 2,
        });
    }

    private static async Task SeedToolArtifactFilesAsync(string runDir, string[] commands)
    {
        var nameContext = await FileNameContext.CreateAsync();

        var rawDir = Path.Combine(runDir, "tools-raw");
        var annotationsDir = Path.Combine(runDir, "annotations");
        var parametersDir = Path.Combine(runDir, "parameters");
        var examplePromptsDir = Path.Combine(runDir, "example-prompts");
        var composedDir = Path.Combine(runDir, "tools-composed");

        foreach (var dir in new[] { rawDir, annotationsDir, parametersDir, examplePromptsDir, composedDir })
            Directory.CreateDirectory(dir);

        foreach (var command in commands)
        {
            var toolFileName = ToolFileNameBuilder.BuildToolFileName(command, nameContext);
            var annotationFileName = ToolFileNameBuilder.BuildAnnotationFileName(command, nameContext);
            var parameterFileName = ToolFileNameBuilder.BuildParameterFileName(command, nameContext);
            var examplePromptsFileName = ToolFileNameBuilder.BuildExamplePromptsFileName(command, nameContext);

            var stub = $"# Smoke test stub for: {command}\n";

            await File.WriteAllTextAsync(Path.Combine(rawDir, toolFileName), stub);
            await File.WriteAllTextAsync(Path.Combine(annotationsDir, annotationFileName), stub);
            await File.WriteAllTextAsync(Path.Combine(parametersDir, parameterFileName), stub);
            await File.WriteAllTextAsync(Path.Combine(examplePromptsDir, examplePromptsFileName), stub);
            // Pre-seed composed files so GetComposedOutputIssues passes (composition runs
            // via RecordingProcessRunner which doesn't create real output files).
            await File.WriteAllTextAsync(Path.Combine(composedDir, toolFileName), stub);
        }
    }
}
