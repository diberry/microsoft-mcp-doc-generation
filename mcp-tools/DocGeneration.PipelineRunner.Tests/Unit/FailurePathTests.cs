using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Steps;
using PipelineRunner.Tests.Fixtures;
using Shared;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Tests that pipeline steps handle failure scenarios correctly:
/// subprocess non-zero exit codes, missing output files, missing input directories,
/// and that all failures produce actionable error messages.
/// </summary>
public class FailurePathTests
{
    // ─────────────────────────────────────────────────────────────────────
    // Step 1: AnnotationsParametersRawStep — subprocess failures
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Step1_AnnotationGeneratorExitCodeNonZero_ReturnsFalseWithExitCodeInWarning()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new FailingProcessRunner(exitCode: 2, standardError: "System.IO.FileNotFoundException: schema.json");
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["storage list"]);
            context.Items["Namespace"] = "storage";

            var step = new AnnotationsParametersRawStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("exit code 2", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, w => w.Contains("FileNotFoundException", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step1_ParameterGeneratorFails_ReturnsFalseEvenIfAnnotationsSucceeded()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var invocationCount = 0;
            var runner = new CallbackProcessRunner();
            runner.OnRun = spec =>
            {
                invocationCount++;
                // First invocation (annotations) succeeds, second (parameters) fails
                return invocationCount == 1
                    ? CallbackProcessRunner.Success(spec)
                    : CallbackProcessRunner.Failure(spec, 1, "Parameter generation crashed");
            };
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["storage list"]);
            context.Items["Namespace"] = "storage";

            SeedFile(Path.Combine(context.OutputPath, "annotations", "azure-storage-list-annotations.md"));

            var step = new AnnotationsParametersRawStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("Parameter generation failed", StringComparison.Ordinal));
            Assert.Equal(2, runner.Invocations.Count); // Stopped after parameter step
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step1_AllProcessesSucceed_EmptyOutputDirectories_FailsValidation()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["storage list"]);
            context.Items["Namespace"] = "storage";

            // Don't seed any output files — directories are empty
            var step = new AnnotationsParametersRawStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("Expected annotations output", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, w => w.Contains("Expected parameters output", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step1_SkipValidation_EmptyOutputDirectories_StillSucceeds()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: true, toolCommands: ["storage list"]);
            context.Items["Namespace"] = "storage";

            var step = new AnnotationsParametersRawStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Step 3: ToolGenerationStep — prerequisite failures
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Step3_MissingPrerequisiteFiles_ReturnsFailureWithPerToolArtifactFailures()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["keyvault list", "keyvault show"]);
            context.Items["Namespace"] = "keyvault";

            // Seed nothing — all prerequisites are missing
            var step = new ToolGenerationStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(2, result.ArtifactFailures.Count);
            Assert.Contains(result.ArtifactFailures, f => f.ArtifactName == "keyvault list");
            Assert.Contains(result.ArtifactFailures, f => f.ArtifactName == "keyvault show");
            Assert.All(result.ArtifactFailures, f =>
                Assert.Contains("prerequisites", f.Summary, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Warnings, w => w.Contains("Missing raw tool prerequisite", StringComparison.Ordinal));
            Assert.Empty(runner.Invocations); // Process never invoked
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step3_CompositionSubprocessFailure_ReturnsStepLevelArtifactFailure()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new FailingProcessRunner(exitCode: 1, standardError: "Unhandled exception: NullReferenceException");
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["cosmos list"]);
            context.Items["Namespace"] = "cosmos";

            await SeedToolGenerationPrerequisitesAsync(context.OutputPath, ["cosmos list"]);

            var step = new ToolGenerationStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            var failure = Assert.Single(result.ArtifactFailures);
            Assert.Equal("pipeline step", failure.ArtifactType);
            Assert.Contains("Composition", failure.ArtifactName, StringComparison.Ordinal);
            Assert.Contains("before specific tools could be identified", failure.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(result.Warnings, w => w.Contains("exit code 1", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step3_CompositionExitsZero_NoComposedFiles_ReturnsPerToolFailure()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["monitor list"]);
            context.Items["Namespace"] = "monitor";

            await SeedToolGenerationPrerequisitesAsync(context.OutputPath, ["monitor list"]);
            // Don't seed composed output — simulates generator producing nothing

            var step = new ToolGenerationStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotEmpty(result.ArtifactFailures);
            Assert.Contains(result.ArtifactFailures, f =>
                f.Summary.Contains("composition", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step3_ImprovementSubprocessFailure_ReturnsStepLevelFailureForImprovement()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var invocationCount = 0;
            var runner = new CallbackProcessRunner();
            runner.OnRun = spec =>
            {
                invocationCount++;
                // Composition succeeds, improvement fails
                return invocationCount == 1
                    ? CallbackProcessRunner.Success(spec)
                    : CallbackProcessRunner.Failure(spec, 1, "AI API timeout");
            };
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["sql list"]);
            context.Items["Namespace"] = "sql";

            await SeedToolGenerationPrerequisitesAsync(context.OutputPath, ["sql list"]);
            await SeedComposedToolOutputAsync(context.OutputPath, "sql list");

            var step = new ToolGenerationStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            var failure = Assert.Single(result.ArtifactFailures);
            Assert.Equal("pipeline step", failure.ArtifactType);
            Assert.Contains("Improvements", failure.ArtifactName, StringComparison.Ordinal);
            Assert.Contains(result.Warnings, w => w.Contains("AI-improved tool generation failed", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step3_ImprovementExitsZero_NoImprovedFiles_CreatesPerToolFailure()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["redis list"]);
            context.Items["Namespace"] = "redis";

            await SeedToolGenerationPrerequisitesAsync(context.OutputPath, ["redis list"]);
            await SeedComposedToolOutputAsync(context.OutputPath, "redis list");
            // Don't seed improved output

            var step = new ToolGenerationStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotEmpty(result.ArtifactFailures);
            Assert.Contains(result.ArtifactFailures, f =>
                f.Summary.Contains("improvement", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step3_CompositionFailsWithIdentifiableFiles_CreatesPerFileFailures()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var nameContext = await FileNameContext.CreateAsync();
            var toolFileName = ToolFileNameBuilder.BuildToolFileName("appservice list", nameContext);
            var runner = new FailingProcessRunner(
                exitCode: 1,
                standardError: $"Error processing {toolFileName}:");
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["appservice list", "appservice show"]);
            context.Items["Namespace"] = "appservice";

            await SeedToolGenerationPrerequisitesAsync(context.OutputPath, ["appservice list", "appservice show"]);

            var step = new ToolGenerationStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            // Should have per-tool failure for appservice list since it matched the regex
            Assert.Contains(result.ArtifactFailures, f => f.ArtifactName == "appservice list");
            Assert.All(result.ArtifactFailures, f => Assert.Equal("tool", f.ArtifactType));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Step 4: ToolFamilyCleanupStep — missing input directories
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Step4_MissingToolsDirectory_ReturnsFailureWithActionableMessage()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute";

            // Don't create the tools directory
            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("Tools directory not found", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, w => w.Contains("Run Step 3 first", StringComparison.Ordinal));
            Assert.Single(result.ArtifactFailures);
            Assert.Equal("tool family", result.ArtifactFailures[0].ArtifactType);
            Assert.Empty(runner.Invocations); // Never runs subprocess
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step4_EmptyToolsDirectory_NoMatchingFiles_ReturnsFailure()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["speech list"]);
            context.Items["Namespace"] = "speech";

            // Create tools directory but with no matching files
            Directory.CreateDirectory(Path.Combine(context.OutputPath, "tools"));

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("No tool files found for family", StringComparison.Ordinal));
            Assert.Single(result.ArtifactFailures);
            Assert.Empty(runner.Invocations);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step4_SubprocessExitCodeNonZero_ReturnsFailureWithExitCodeAndStderr()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new FailingProcessRunner(exitCode: 3, standardError: "Handlebars template error: missing partial");
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute";

            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("Tool-family cleanup failed", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, w => w.Contains("exit code 3", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, w => w.Contains("Handlebars template error", StringComparison.Ordinal));
            Assert.Single(result.ArtifactFailures);
            Assert.Equal("tool family", result.ArtifactFailures[0].ArtifactType);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step4_SubprocessExitsZero_NoOutputFiles_SurfacesDiagnosticHint()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute";

            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");

            var step = new ToolFamilyCleanupStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w =>
                w.Contains("Subprocess exited 0 but produced no output files", StringComparison.Ordinal));
            Assert.Single(result.ArtifactFailures);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Step 6: HorizontalArticlesStep — failure paths
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Step6_MissingCliVersionFile_ReturnsFailureWithActionableMessage()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["advisor list"]);
            context.Items["Namespace"] = "advisor";

            // Don't create cli-version.json
            var step = new HorizontalArticlesStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("CLI version file not found", StringComparison.Ordinal));
            Assert.Single(result.ArtifactFailures);
            Assert.Equal("horizontal article", result.ArtifactFailures[0].ArtifactType);
            Assert.Empty(runner.Invocations); // Should not invoke subprocess
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step6_SubprocessExitCodeNonZero_ReturnsFailureWithProcessDiagnostics()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new FailingProcessRunner(exitCode: 1, standardError: "Azure OpenAI returned 429 Too Many Requests");
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["eventgrid list"]);
            context.Items["Namespace"] = "eventgrid";

            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");

            var step = new HorizontalArticlesStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("Horizontal article generation failed", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, w => w.Contains("exit code 1", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, w => w.Contains("429 Too Many Requests", StringComparison.Ordinal));
            Assert.Single(result.ArtifactFailures);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step6_SubprocessSucceeds_ErrorArtifactExists_ReturnsFailure()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["servicebus list"]);
            context.Items["Namespace"] = "servicebus";

            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");
            // Subprocess exit 0 but leaves an error artifact
            SeedFile(Path.Combine(context.OutputPath, "horizontal-articles", "error-servicebus.txt"), "AI returned malformed JSON");

            var step = new HorizontalArticlesStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("error artifact", StringComparison.OrdinalIgnoreCase));
            Assert.Single(result.ArtifactFailures);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step6_SubprocessSucceeds_NoArticleOutput_ReturnsFailure()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["loadtesting list"]);
            context.Items["Namespace"] = "loadtesting";

            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");
            // Subprocess succeeds but writes no article file

            var step = new HorizontalArticlesStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("Expected horizontal article output", StringComparison.Ordinal));
            Assert.Single(result.ArtifactFailures);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step6_SkipValidation_MissingCliVersion_DoesNotBlockExecution()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: true, toolCommands: ["advisor list"]);
            context.Items["Namespace"] = "advisor";

            SeedFile(Path.Combine(context.OutputPath, "horizontal-articles", "horizontal-article-advisor.md"));

            var step = new HorizontalArticlesStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(runner.Invocations); // Subprocess was invoked
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Step 5: SkillsRelevanceStep — warn-only failures
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Step5_SubprocessExitsNonZero_ReturnsFailureWithActionableDiagnostics()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new FailingProcessRunner(exitCode: 5, standardError: "HttpRequestException: Name resolution failed");
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["functionapp list"]);
            context.Items["Namespace"] = "functionapp";

            var step = new SkillsRelevanceStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("Skills relevance generation failed (exit code 5).", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, w => w.Contains("HttpRequestException", StringComparison.Ordinal));
            Assert.Single(result.ArtifactFailures);
            Assert.Equal("azure skill", result.ArtifactFailures[0].ArtifactType);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step5_SubprocessSucceeds_MissingOutput_ReturnsFailureWithPath()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["acr list"]);
            context.Items["Namespace"] = "acr";

            // Don't seed the expected output file
            var step = new SkillsRelevanceStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("Expected skills relevance output", StringComparison.Ordinal));
            Assert.Single(result.ArtifactFailures);
            Assert.Contains("missing", result.ArtifactFailures[0].Summary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Cross-cutting: ResolveTarget / context validation
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnyStep_MissingNamespaceInContext_ThrowsInvalidOperation()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            // Deliberately do NOT set context.Items["Namespace"]

            var step = new AnnotationsParametersRawStep();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => step.ExecuteAsync(context, CancellationToken.None).AsTask());
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task AnyStep_NullCliOutput_ThrowsInvalidOperation()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute";
            context.CliOutput = null; // Simulate missing CLI metadata

            var step = new AnnotationsParametersRawStep();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => step.ExecuteAsync(context, CancellationToken.None).AsTask());
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task AnyStep_EmptyCliVersion_ThrowsInvalidOperation()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute";
            context.CliVersion = "   "; // Whitespace-only

            var step = new AnnotationsParametersRawStep();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => step.ExecuteAsync(context, CancellationToken.None).AsTask());
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // BuildResult contract: warnings are filtered, outputs are resolved
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Step1_FailureResult_IncludesProcessInvocationsForDiagnostics()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new FailingProcessRunner(exitCode: 1, standardError: "crash");
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute";

            var step = new AnnotationsParametersRawStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotEmpty(result.ProcessInvocations);
            Assert.Contains(result.ProcessInvocations, cmd => cmd.Contains("dotnet", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step3_FailureResult_ArtifactFailureIncludesRelatedPaths()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute";

            // Missing prerequisites → artifact failures with related paths
            var step = new ToolGenerationStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.NotEmpty(result.ArtifactFailures);
            Assert.All(result.ArtifactFailures, f =>
            {
                Assert.NotEmpty(f.RelatedPaths);
                Assert.All(f.RelatedPaths, p => Assert.False(string.IsNullOrWhiteSpace(p)));
            });
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers (mirrored from NamespaceStepTests)
    // ─────────────────────────────────────────────────────────────────────

    private static PipelineContext CreateContext(string testRoot, IProcessRunner processRunner, bool skipValidation, IReadOnlyList<string> toolCommands)
    {
        var mcpToolsRoot = Path.Combine(testRoot, "mcp-tools");
        var outputPath = Path.Combine(testRoot, "generated-compute");
        Directory.CreateDirectory(mcpToolsRoot);
        Directory.CreateDirectory(outputPath);

        return new PipelineContext
        {
            Request = new PipelineRequest("compute", [1], outputPath, SkipBuild: true, SkipValidation: skipValidation, DryRun: false),
            RepoRoot = testRoot,
            McpToolsRoot = mcpToolsRoot,
            OutputPath = outputPath,
            ProcessRunner = processRunner,
            Workspaces = new WorkspaceManager(),
            CliMetadataLoader = new StubCliMetadataLoader(),
            TargetMatcher = new TargetMatcher(),
            FilteredCliWriter = new FilteredCliWriter(new WorkspaceManager()),
            BuildCoordinator = new StubBuildCoordinator(),
            AiCapabilityProbe = new StubAiCapabilityProbe(),
            Reports = new BufferedReportWriter(),
            CliVersion = "1.2.3",
            CliOutput = CreateSnapshot(toolCommands),
            SelectedNamespaces = ["compute"],
        };
    }

    private static CliMetadataSnapshot CreateSnapshot(IReadOnlyList<string> toolCommands)
    {
        var json = JsonSerializer.Serialize(new
        {
            version = "1.2.3",
            results = toolCommands.Select(command => new
            {
                command,
                name = command,
                description = $"Description for {command}",
            }),
        });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement.Clone();
        var tools = root.GetProperty("results")
            .EnumerateArray()
            .Select(tool => new CliTool(
                tool.GetProperty("command").GetString() ?? string.Empty,
                tool.GetProperty("name").GetString() ?? string.Empty,
                tool.GetProperty("description").GetString(),
                tool.Clone()))
            .ToArray();

        return new CliMetadataSnapshot(Path.Combine(Path.GetTempPath(), $"cli-output-{Guid.NewGuid():N}.json"), root, tools);
    }

    private static async Task SeedToolGenerationPrerequisitesAsync(string outputPath, IReadOnlyList<string> commands)
    {
        var nameContext = await FileNameContext.CreateAsync();
        foreach (var command in commands)
        {
            var toolFileName = ToolFileNameBuilder.BuildToolFileName(command, nameContext);
            SeedFile(Path.Combine(outputPath, "tools-raw", toolFileName));
            SeedFile(Path.Combine(outputPath, "annotations", ToolFileNameBuilder.BuildAnnotationFileName(command, nameContext)));
            SeedFile(Path.Combine(outputPath, "parameters", ToolFileNameBuilder.BuildParameterFileName(command, nameContext)));
            SeedFile(Path.Combine(outputPath, "example-prompts", ToolFileNameBuilder.BuildExamplePromptsFileName(command, nameContext)));
        }
    }

    private static async Task SeedComposedToolOutputAsync(string outputPath, string command)
    {
        var nameContext = await FileNameContext.CreateAsync();
        var toolFileName = ToolFileNameBuilder.BuildToolFileName(command, nameContext);
        SeedFile(Path.Combine(outputPath, "tools-composed", toolFileName));
    }

    private static string CreateTestRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"failure-path-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void SeedFile(string path, string content = "content")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void SeedToolFile(string path, string command)
        => SeedFile(path, $"---\n---\n# Sample\n\n<!-- @mcpcli {command} -->\nbody\n");

    private static void DeleteTestRoot(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class CallbackProcessRunner : IProcessRunner
    {
        public List<ProcessSpec> Invocations { get; } = new();

        public Func<ProcessSpec, ProcessExecutionResult>? OnRun { get; set; }

        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
        {
            Invocations.Add(spec);
            return ValueTask.FromResult(OnRun?.Invoke(spec) ?? Success(spec));
        }

        public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken cancellationToken)
            => RunAsync(new ProcessSpec("dotnet", ["build", solutionPath], Path.GetDirectoryName(solutionPath) ?? string.Empty), cancellationToken);

        public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(string projectPath, IEnumerable<string> arguments, bool noBuild, string workingDirectory, CancellationToken cancellationToken)
        {
            var invocation = new List<string>
            {
                "run",
                "--project",
                projectPath,
                "--configuration",
                "Release",
            };

            if (noBuild)
            {
                invocation.Add("--no-build");
            }

            invocation.Add("--");
            invocation.AddRange(arguments);
            return RunAsync(new ProcessSpec("dotnet", invocation, workingDirectory), cancellationToken);
        }

        public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(string scriptPath, IEnumerable<string> arguments, string workingDirectory, CancellationToken cancellationToken)
            => RunAsync(new ProcessSpec("pwsh", ["-File", scriptPath, .. arguments], workingDirectory), cancellationToken);

        public static ProcessExecutionResult Success(ProcessSpec spec)
            => new(spec.FileName, spec.Arguments, spec.WorkingDirectory, 0, string.Empty, string.Empty, TimeSpan.Zero);

        public static ProcessExecutionResult Failure(ProcessSpec spec, int exitCode, string standardError)
            => new(spec.FileName, spec.Arguments, spec.WorkingDirectory, exitCode, string.Empty, standardError, TimeSpan.Zero);
    }

    private sealed class FailingProcessRunner : IProcessRunner
    {
        private readonly int _exitCode;
        private readonly string _standardError;
        private readonly string _standardOutput;

        public FailingProcessRunner(int exitCode, string standardError, string standardOutput = "")
        {
            _exitCode = exitCode;
            _standardError = standardError;
            _standardOutput = standardOutput;
        }

        public List<ProcessSpec> Invocations { get; } = new();

        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
        {
            Invocations.Add(spec);
            return ValueTask.FromResult(new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory, _exitCode, _standardOutput, _standardError, TimeSpan.Zero));
        }

        public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken cancellationToken)
            => RunAsync(
                new ProcessSpec(
                    "dotnet",
                    ["build", solutionPath, "--configuration", "Release", "--verbosity", "quiet"],
                    Path.GetDirectoryName(solutionPath) ?? Environment.CurrentDirectory),
                cancellationToken);

        public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(string projectPath, IEnumerable<string> arguments, bool noBuild, string workingDirectory, CancellationToken cancellationToken)
        {
            var invocation = new List<string>
            {
                "run",
                "--project",
                projectPath,
                "--configuration",
                "Release",
            };

            if (noBuild)
            {
                invocation.Add("--no-build");
            }

            invocation.Add("--");
            invocation.AddRange(arguments);
            return RunAsync(new ProcessSpec("dotnet", invocation, workingDirectory), cancellationToken);
        }

        public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(string scriptPath, IEnumerable<string> arguments, string workingDirectory, CancellationToken cancellationToken)
            => RunAsync(new ProcessSpec("pwsh", ["-File", scriptPath, .. arguments], workingDirectory), cancellationToken);
    }
}
