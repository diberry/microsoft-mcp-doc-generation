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

public class NamespaceStepTests
{
    [Fact]
    public async Task Step1_AnnotationsParametersRaw_UsesExpectedGeneratorArguments()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";

            SeedFile(Path.Combine(context.OutputPath, "annotations", "compute-list-annotations.md"));
            SeedFile(Path.Combine(context.OutputPath, "parameters", "compute-list-parameters.md"));
            SeedFile(Path.Combine(context.OutputPath, "tools-raw", "compute-list.md"));

            var step = new AnnotationsParametersRawStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(3, runner.Invocations.Count);
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "generate-docs");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--annotations");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--no-build");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument.EndsWith("cli-output-single-tool.json", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[1].Arguments, argument => argument == "--parameters");
            Assert.Contains(runner.Invocations[2].Arguments, argument => argument.EndsWith("DocGeneration.Steps.AnnotationsParametersRaw.RawTools.csproj", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[2].Arguments, argument => argument == Path.Combine(context.OutputPath, "tools-raw"));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step2_ExamplePrompts_UsesGeneratorAndValidatorArguments()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute list";

            await SeedExamplePromptOutputsAsync(context.OutputPath, "compute list");
            SeedFile(Path.Combine(context.OutputPath, "e2e-test-prompts", "parsed.json"), "{}");
            Directory.CreateDirectory(Path.Combine(context.OutputPath, "parameters"));

            var step = new ExamplePromptsStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(2, runner.Invocations.Count);
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument.EndsWith("DocGeneration.Steps.ExamplePrompts.Generation.csproj", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--e2e-prompts");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--param-manifests");
            Assert.Contains(runner.Invocations[1].Arguments, argument => argument.EndsWith("DocGeneration.Steps.ExamplePrompts.Validation.csproj", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[1].Arguments, argument => argument == "--tool-command");
            Assert.Contains(runner.Invocations[1].Arguments, argument => argument == "compute list");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step2_ExamplePrompts_NamespaceRun_UsesValidatorWithoutToolFilter()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";

            await SeedExamplePromptOutputsAsync(context.OutputPath, "compute list", "compute show");
            Directory.CreateDirectory(Path.Combine(context.OutputPath, "parameters"));

            var step = new ExamplePromptsStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(2, runner.Invocations.Count);
            Assert.Contains(runner.Invocations[1].Arguments, argument => argument.EndsWith("DocGeneration.Steps.ExamplePrompts.Validation.csproj", StringComparison.Ordinal));
            Assert.DoesNotContain("--tool-command", runner.Invocations[1].Arguments);
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("Skipping example prompt validation", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step2_ExamplePrompts_MissingToolOutputCreatesArtifactFailure()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";

            await SeedExamplePromptOutputsAsync(context.OutputPath, "compute list");
            Directory.CreateDirectory(Path.Combine(context.OutputPath, "parameters"));

            var step = new ExamplePromptsStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Single(result.ArtifactFailures);
            Assert.Equal("compute show", result.ArtifactFailures[0].ArtifactName);
            Assert.Contains("incomplete", result.ArtifactFailures[0].Summary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step2_ExamplePrompts_ValidatorFailureCreatesArtifactFailureWithoutBlockingStep()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new ExamplePromptRetryRunner("compute list", validationFailuresBeforeSuccess: 3);
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute list";

            await SeedExamplePromptOutputsAsync(context.OutputPath, "compute list");
            Directory.CreateDirectory(Path.Combine(context.OutputPath, "parameters"));

            var step = new ExamplePromptsStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(result.ArtifactFailures);
            Assert.Equal("compute list", result.ArtifactFailures[0].ArtifactName);
            Assert.Contains("after automatic retries", result.ArtifactFailures[0].Summary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step2_ExamplePrompts_ValidatorFailure_RetriesInvalidToolWithFeedback()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new ExamplePromptRetryRunner("compute list", validationFailuresBeforeSuccess: 1);
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute list";

            var liveArtifacts = await GetExamplePromptArtifactsAsync(context.OutputPath, "compute list");
            SeedFile(liveArtifacts.ExamplePromptPath, "initial example prompts");
            SeedFile(liveArtifacts.InputPromptPath, "initial input prompt");
            SeedFile(liveArtifacts.RawOutputPath, "initial raw output");
            Directory.CreateDirectory(Path.Combine(context.OutputPath, "parameters"));

            var step = new ExamplePromptsStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);
            var attemptOneArtifacts = await GetExamplePromptArtifactsAsync(context.OutputPath, "compute list", attempt: 1);
            var feedbackPath = GetArgumentValue(runner.Invocations[2].Arguments, "--validation-feedback-file");

            Assert.True(result.Success);
            Assert.Empty(result.ArtifactFailures);
            Assert.Equal(4, runner.Invocations.Count);
            Assert.Contains(result.Warnings, warning => warning.Contains("Retrying example prompts for 'compute list' (attempt 1/2)", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[2].Arguments, argument => argument == "--tool-command");
            Assert.Contains(runner.Invocations[2].Arguments, argument => argument == "compute list");
            Assert.Equal(attemptOneArtifacts.ValidationPath, feedbackPath);
            Assert.True(File.Exists(attemptOneArtifacts.ExamplePromptPath));
            Assert.True(File.Exists(attemptOneArtifacts.InputPromptPath));
            Assert.True(File.Exists(attemptOneArtifacts.RawOutputPath));
            Assert.True(File.Exists(attemptOneArtifacts.ValidationPath));
            Assert.Contains("Attempt 1", await File.ReadAllTextAsync(attemptOneArtifacts.ValidationPath), StringComparison.Ordinal);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step2_ExamplePrompts_RetryGenerationFailureCreatesArtifactFailure()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new ExamplePromptRetryRunner(
                "compute list",
                validationFailuresBeforeSuccess: 3,
                failingRetryGenerationAttempts: [1, 2]);
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute list";

            await SeedExamplePromptOutputsAsync(context.OutputPath, "compute list");
            Directory.CreateDirectory(Path.Combine(context.OutputPath, "parameters"));

            var step = new ExamplePromptsStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            var failure = Assert.Single(result.ArtifactFailures);
            Assert.Equal("compute list", failure.ArtifactName);
            Assert.Contains("after automatic retries", failure.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(result.Warnings, warning => warning.Contains("Example prompt regeneration failed for 'compute list'", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step2_ExamplePrompts_RetryMissingRegeneratedOutputCreatesArtifactFailure()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new ExamplePromptRetryRunner(
                "compute list",
                validationFailuresBeforeSuccess: 3,
                missingOutputRetryAttempts: [1, 2]);
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute list";

            await SeedExamplePromptOutputsAsync(context.OutputPath, "compute list");
            Directory.CreateDirectory(Path.Combine(context.OutputPath, "parameters"));

            var step = new ExamplePromptsStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            var failure = Assert.Single(result.ArtifactFailures);
            Assert.Equal("compute list", failure.ArtifactName);
            Assert.Contains("after automatic retries", failure.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(result.Warnings, warning => warning.Contains("Missing example prompts markdown", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step2_ExamplePrompts_ValidatorFailureAfterRetriesCreatesArtifactFailure()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new ExamplePromptRetryRunner("compute list", validationFailuresBeforeSuccess: 3);
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute list";

            await SeedExamplePromptOutputsAsync(context.OutputPath, "compute list");
            Directory.CreateDirectory(Path.Combine(context.OutputPath, "parameters"));

            var step = new ExamplePromptsStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            var failure = Assert.Single(result.ArtifactFailures);
            Assert.Equal("compute list", failure.ArtifactName);
            Assert.Contains("after automatic retries", failure.Summary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(result.Warnings, warning => warning.Contains("attempt 2/2", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step2_ExamplePrompts_TextFailureMarkersCreateToolArtifactFailure()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new FailingProcessRunner(1, "generator failed", standardOutput: "[FAILED] compute list");
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";
            Directory.CreateDirectory(Path.Combine(context.OutputPath, "parameters"));

            var step = new ExamplePromptsStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            var failure = Assert.Single(result.ArtifactFailures);
            Assert.Equal("tool", failure.ArtifactType);
            Assert.Equal("compute list", failure.ArtifactName);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step2_ExamplePrompts_GeneratorCrashCreatesStepLevelArtifactFailure()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new FailingProcessRunner(1, "Unhandled exception: generator crashed");
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";
            Directory.CreateDirectory(Path.Combine(context.OutputPath, "parameters"));

            var step = new ExamplePromptsStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            var failure = Assert.Single(result.ArtifactFailures);
            Assert.Equal("pipeline step", failure.ArtifactType);
            Assert.Equal("DocGeneration.Steps.ExamplePrompts.Generation", failure.ArtifactName);
            Assert.Contains("before specific tools could be identified", failure.Summary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step3_ToolGeneration_UsesExpectedGeneratorArguments()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";

            await SeedToolGenerationFilesAsync(context.OutputPath, ["compute list", "compute show"], includeComposed: true, includeImproved: true);

            var step = new ToolGenerationStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(2, runner.Invocations.Count);
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument.EndsWith("DocGeneration.Steps.ToolGeneration.Composition.csproj", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == Path.Combine(context.OutputPath, "tools-raw"));
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == Path.Combine(context.OutputPath, "example-prompts"));
            Assert.Contains(runner.Invocations[1].Arguments, argument => argument.EndsWith("DocGeneration.Steps.ToolGeneration.Improvements.csproj", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[1].Arguments, argument => argument == "8000");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step3_ToolGeneration_MissingImprovedToolCreatesArtifactFailure()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";

            await SeedToolGenerationFilesAsync(context.OutputPath, ["compute list", "compute show"], includeComposed: true, includeImproved: false);
            await SeedToolOutputAsync(context.OutputPath, "compute list", includeComposed: false, includeImproved: true);

            var step = new ToolGenerationStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Single(result.ArtifactFailures);
            Assert.Equal("compute show", result.ArtifactFailures[0].ArtifactName);
            Assert.Contains("improvement", result.ArtifactFailures[0].Summary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step3_ToolGeneration_GeneratorCrashCreatesStepLevelArtifactFailure()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new FailingProcessRunner(1, "Unhandled exception: tool generation crashed");
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";

            await SeedToolGenerationFilesAsync(context.OutputPath, ["compute list", "compute show"], includeComposed: false, includeImproved: false);

            var step = new ToolGenerationStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            var failure = Assert.Single(result.ArtifactFailures);
            Assert.Equal("pipeline step", failure.ArtifactType);
            Assert.Equal("DocGeneration.Steps.ToolGeneration.Composition", failure.ArtifactName);
            Assert.Contains("before specific tools could be identified", failure.Summary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void CriticalFailureRecorder_AnnotationsStepFallbackCreatesPerToolRecords()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot, new RecordingProcessRunner(), skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";
            var step = new AnnotationsParametersRawStep();
            var result = new StepResult(false, ["Raw generation failed"], TimeSpan.Zero, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ValidatorResult>(), Array.Empty<ArtifactFailure>());

            var persisted = CriticalFailureRecorder.Persist(context, step, result);

            Assert.Equal(2, persisted.Count);
            Assert.Contains(persisted, reference => reference.ArtifactName == "compute list");
            Assert.Contains(persisted, reference => reference.ArtifactName == "compute show");
            Assert.All(persisted, reference => Assert.True(File.Exists(reference.RecordPath)));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void CriticalFailureRecorder_TruncatesSanitizedArtifactNamesInFileNames()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot, new RecordingProcessRunner(), skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute list";
            var step = new ExamplePromptsStep();
            var result = new StepResult(
                false,
                Array.Empty<string>(),
                TimeSpan.Zero,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<ValidatorResult>(),
                [ArtifactFailure.Create("tool", new string('a', 140), "summary")]);

            var persisted = CriticalFailureRecorder.Persist(context, step, result);

            var record = Assert.Single(persisted);
            var fileName = Path.GetFileNameWithoutExtension(record.RecordPath);
            var start = fileName.IndexOf("-tool-", StringComparison.Ordinal);
            var end = fileName.LastIndexOf("-01", StringComparison.Ordinal);
            var artifactSegment = fileName[(start + "-tool-".Length)..end];
            Assert.True(artifactSegment.Length <= 100, $"Expected sanitized artifact segment to be at most 100 characters but was {artifactSegment.Length}.");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public void CriticalFailureRecorder_WriteFailuresDoNotThrowWhenDirectoryIsBlocked()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot, new RecordingProcessRunner(), skipValidation: false, toolCommands: ["compute list"]);
            context.Items["Namespace"] = "compute list";
            File.WriteAllText(Path.Combine(context.OutputPath, "critical-failures"), "blocked");
            var step = new ExamplePromptsStep();
            var result = new StepResult(
                false,
                Array.Empty<string>(),
                TimeSpan.Zero,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<ValidatorResult>(),
                [ArtifactFailure.Create("tool", "compute list", "summary")]);

            var persisted = CriticalFailureRecorder.Persist(context, step, result);

            Assert.Empty(persisted);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step5_SkillsRelevance_UsesExpectedGeneratorArguments()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";

            var skillsOutputDirectory = Path.Combine(context.OutputPath, "skills-relevance");
            SeedFile(Path.Combine(skillsOutputDirectory, "compute-skills-relevance.md"));

            var step = new SkillsRelevanceStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(runner.Invocations);
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument.EndsWith("DocGeneration.Steps.SkillsRelevance.csproj", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "compute");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--output-path");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == skillsOutputDirectory);
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--min-score");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "0.1");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step5_SkillsRelevance_FailureReturnsWarningsWithoutThrowing()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new FailingProcessRunner(7, "GitHub API rate limited");
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";

            var step = new SkillsRelevanceStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Single(runner.Invocations);
            Assert.Contains(result.Warnings, warning => warning.Contains("Skills relevance generation failed (exit code 7).", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, warning => warning.Contains("GitHub API rate limited", StringComparison.Ordinal));
            Assert.Single(result.ArtifactFailures);
            Assert.Equal("compute-skills-relevance.md", result.ArtifactFailures[0].ArtifactName);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task Step6_HorizontalArticles_UsesExpectedGeneratorArguments()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var runner = new RecordingProcessRunner();
            var context = CreateContext(testRoot, runner, skipValidation: false, toolCommands: ["compute list", "compute show"]);
            context.Items["Namespace"] = "compute";

            SeedFile(Path.Combine(context.OutputPath, "cli", "cli-version.json"), "{\"version\":\"1.2.3\"}");
            SeedFile(Path.Combine(context.OutputPath, "horizontal-articles", "horizontal-article-compute.md"));

            var step = new HorizontalArticlesStep();
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Single(runner.Invocations);
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument.EndsWith("DocGeneration.Steps.HorizontalArticles.csproj", StringComparison.Ordinal));
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--single-service");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "compute");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--output-path");
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == context.OutputPath);
            Assert.Contains(runner.Invocations[0].Arguments, argument => argument == "--transform");
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static PipelineContext CreateContext(string testRoot, IProcessRunner processRunner, bool skipValidation, IReadOnlyList<string> toolCommands)
    {
        var docsGenerationRoot = Path.Combine(testRoot, "mcp-tools");
        var outputPath = Path.Combine(testRoot, "generated-compute");
        Directory.CreateDirectory(docsGenerationRoot);
        Directory.CreateDirectory(outputPath);

        return new PipelineContext
        {
            Request = new PipelineRequest("compute", [1], outputPath, SkipBuild: true, SkipValidation: skipValidation, DryRun: false),
            RepoRoot = testRoot,
            DocsGenerationRoot = docsGenerationRoot,
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

    private static async Task<ExamplePromptArtifacts> GetExamplePromptArtifactsAsync(string outputPath, string command, int? attempt = null)
    {
        var nameContext = await FileNameContext.CreateAsync();
        var attemptDirectory = attempt.HasValue ? $"attempt-{attempt.Value}" : null;

        var examplePromptsDirectory = Path.Combine(outputPath, "example-prompts");
        var inputPromptsDirectory = Path.Combine(outputPath, "example-prompts-prompts");
        var rawOutputDirectory = Path.Combine(outputPath, "example-prompts-raw-output");
        var validationDirectory = Path.Combine(outputPath, "example-prompts-validation");
        if (!string.IsNullOrWhiteSpace(attemptDirectory))
        {
            examplePromptsDirectory = Path.Combine(examplePromptsDirectory, attemptDirectory);
            inputPromptsDirectory = Path.Combine(inputPromptsDirectory, attemptDirectory);
            rawOutputDirectory = Path.Combine(rawOutputDirectory, attemptDirectory);
            validationDirectory = Path.Combine(validationDirectory, attemptDirectory);
        }

        return new ExamplePromptArtifacts(
            Path.Combine(examplePromptsDirectory, ToolFileNameBuilder.BuildExamplePromptsFileName(command, nameContext)),
            Path.Combine(inputPromptsDirectory, ToolFileNameBuilder.BuildInputPromptFileName(command, nameContext)),
            Path.Combine(rawOutputDirectory, ToolFileNameBuilder.BuildRawOutputFileName(command, nameContext)),
            Path.Combine(validationDirectory, $"{ToolFileNameBuilder.BuildBaseFileName(command, nameContext)}-validation.md"));
    }

    private static async Task SeedExamplePromptOutputsAsync(string outputPath, params string[] commands)
    {
        foreach (var command in commands)
        {
            var artifacts = await GetExamplePromptArtifactsAsync(outputPath, command);
            SeedFile(artifacts.ExamplePromptPath);
            SeedFile(artifacts.InputPromptPath);
            SeedFile(artifacts.RawOutputPath);
            SeedFile(artifacts.ValidationPath);
        }
    }

    private static async Task SeedToolGenerationFilesAsync(string outputPath, IReadOnlyList<string> commands, bool includeComposed, bool includeImproved)
    {
        foreach (var command in commands)
        {
            await SeedToolOutputAsync(outputPath, command, includeComposed, includeImproved);
        }
    }

    private static async Task SeedToolOutputAsync(string outputPath, string command, bool includeComposed, bool includeImproved)
    {
        var nameContext = await FileNameContext.CreateAsync();
        var toolFileName = ToolFileNameBuilder.BuildToolFileName(command, nameContext);
        SeedFile(Path.Combine(outputPath, "tools-raw", toolFileName));
        SeedFile(Path.Combine(outputPath, "annotations", ToolFileNameBuilder.BuildAnnotationFileName(command, nameContext)));
        SeedFile(Path.Combine(outputPath, "parameters", ToolFileNameBuilder.BuildParameterFileName(command, nameContext)));
        SeedFile(Path.Combine(outputPath, "example-prompts", ToolFileNameBuilder.BuildExamplePromptsFileName(command, nameContext)));

        if (includeComposed)
        {
            SeedFile(Path.Combine(outputPath, "tools-composed", toolFileName));
        }

        if (includeImproved)
        {
            SeedFile(Path.Combine(outputPath, "tools", toolFileName));
        }
    }

    private static string CreateTestRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pipeline-runner-step-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void SeedFile(string path, string content = "content")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void DeleteTestRoot(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string? GetArgumentValue(IReadOnlyList<string> arguments, string flag)
    {
        for (var i = 0; i < arguments.Count - 1; i++)
        {
            if (arguments[i].Equals(flag, StringComparison.Ordinal))
            {
                return arguments[i + 1];
            }
        }

        return null;
    }

    private sealed record ExamplePromptArtifacts(
        string ExamplePromptPath,
        string InputPromptPath,
        string RawOutputPath,
        string ValidationPath);

    private sealed class ExamplePromptRetryRunner : IProcessRunner
    {
        private readonly string _invalidTool;
        private readonly HashSet<int> _failingRetryGenerationAttempts;
        private readonly HashSet<int> _missingOutputRetryAttempts;
        private int _validationFailuresRemaining;
        private int _retryGenerationAttempts;
        private int _validationAttempts;

        public ExamplePromptRetryRunner(
            string invalidTool,
            int validationFailuresBeforeSuccess,
            IEnumerable<int>? failingRetryGenerationAttempts = null,
            IEnumerable<int>? missingOutputRetryAttempts = null)
        {
            _invalidTool = invalidTool;
            _validationFailuresRemaining = validationFailuresBeforeSuccess;
            _failingRetryGenerationAttempts = failingRetryGenerationAttempts?.ToHashSet() ?? [];
            _missingOutputRetryAttempts = missingOutputRetryAttempts?.ToHashSet() ?? [];
        }

        public List<ProcessSpec> Invocations { get; } = new();

        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
        {
            Invocations.Add(spec);

            var projectPath = GetArgumentValue(spec.Arguments, "--project");
            if (projectPath?.EndsWith("DocGeneration.Steps.ExamplePrompts.Generation.csproj", StringComparison.Ordinal) == true
                && spec.Arguments.Contains("--tool-command", StringComparer.Ordinal))
            {
                _retryGenerationAttempts++;
                var outputPath = GetOutputPath(spec.Arguments)!;

                if (_missingOutputRetryAttempts.Contains(_retryGenerationAttempts))
                {
                    DeleteRetryOutput(outputPath, _invalidTool);
                }

                if (_failingRetryGenerationAttempts.Contains(_retryGenerationAttempts))
                {
                    var stdout = $"❌ {_invalidTool} (retry {_retryGenerationAttempts})";
                    var stderr = $"generator retry {_retryGenerationAttempts} failed";
                    return ValueTask.FromResult(new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory, 1, stdout, stderr, TimeSpan.Zero));
                }
            }

            if (projectPath?.EndsWith("DocGeneration.Steps.ExamplePrompts.Validation.csproj", StringComparison.Ordinal) == true)
            {
                var generatedPath = GetArgumentValue(spec.Arguments, "--generated")!;
                _validationAttempts++;
                WriteValidationFile(generatedPath, _invalidTool, _validationAttempts);

                if (_validationFailuresRemaining > 0)
                {
                    _validationFailuresRemaining--;
                    var stdout = $"Validation Summary{Environment.NewLine}------------------{Environment.NewLine}Invalid tools:{Environment.NewLine}  - {_invalidTool}";
                    return ValueTask.FromResult(new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory, 1, stdout, string.Empty, TimeSpan.Zero));
                }
            }

            return ValueTask.FromResult(new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory, 0, string.Empty, string.Empty, TimeSpan.Zero));
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

        private static string? GetOutputPath(IReadOnlyList<string> arguments)
        {
            for (var i = 0; i < arguments.Count; i++)
            {
                if (arguments[i].Equals("--", StringComparison.Ordinal) && i + 2 < arguments.Count)
                {
                    return arguments[i + 2];
                }
            }

            return null;
        }

        private static void DeleteRetryOutput(string outputPath, string command)
        {
            var artifacts = GetExamplePromptArtifactsAsync(outputPath, command).GetAwaiter().GetResult();
            if (File.Exists(artifacts.ExamplePromptPath))
            {
                File.Delete(artifacts.ExamplePromptPath);
            }
        }

        private static void WriteValidationFile(string generatedPath, string command, int validationAttempt)
        {
            var artifacts = GetExamplePromptArtifactsAsync(generatedPath, command).GetAwaiter().GetResult();
            Directory.CreateDirectory(Path.GetDirectoryName(artifacts.ValidationPath)!);
            File.WriteAllText(
                artifacts.ValidationPath,
                $"# Example Prompt Validation: {command}{Environment.NewLine}{Environment.NewLine}**Status:** Invalid{Environment.NewLine}**Summary:** Attempt {validationAttempt} missing required parameters or quoting issues{Environment.NewLine}- Missing params: subscription{Environment.NewLine}- Issue: Use quoted placeholders");
        }
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
