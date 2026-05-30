using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Shared;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class PipelineRunnerPostValidatorTests
{
    [Fact]
    public async Task RunAsync_PostValidatorFailureStopsPipeline()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-validator-hook-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var processRunner = new RecordingProcessRunner();
            var reportWriter = new BufferedReportWriter();
            var contextFactory = new PipelineContextFactory(
                processRunner,
                new WorkspaceManager(),
                new StaticCliMetadataLoader(),
                new TargetMatcher(),
                new StubFilteredCliWriter(),
                new StubBuildCoordinator(),
                new StubAiCapabilityProbe(),
                reportWriter,
                repoRoot);

            var step = new RecordingStep(
                id: 4,
                name: "Generate tool-family article",
                failurePolicy: FailurePolicy.Fatal,
                success: true,
                postValidators: [new FixedValidator(success: false, warnings: ["validator failed"])]);
            var runner = new global::PipelineRunner.PipelineRunner(new StepRegistry([step]), contextFactory);

            var request = new PipelineRequest("compute", [4], ".\\generated-compute", SkipBuild: true, SkipValidation: false, DryRun: false);
            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.FatalExitCode, exitCode);
            Assert.Equal(1, step.Executions);
            Assert.Contains(reportWriter.Messages, message => message.Contains("Validator: FixedValidator", StringComparison.Ordinal));
            Assert.Contains(reportWriter.Messages, message => message.Contains("validator failed", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_PostValidatorFailure_RetriesStepWithMaxRetries()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-retry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var processRunner = new RecordingProcessRunner();
            var reportWriter = new BufferedReportWriter();
            var contextFactory = new PipelineContextFactory(
                processRunner,
                new WorkspaceManager(),
                new StaticCliMetadataLoader(),
                new TargetMatcher(),
                new StubFilteredCliWriter(),
                new StubBuildCoordinator(),
                new StubAiCapabilityProbe(),
                reportWriter,
                repoRoot);

            // Step with maxRetries=2 and validator that always fails
            var step = new RecordingStep(
                id: 4,
                name: "Generate tool-family article",
                failurePolicy: FailurePolicy.Fatal,
                success: true,
                maxRetries: 2,
                postValidators: [new FixedValidator(success: false, warnings: ["validator failed"])]);
            var runner = new global::PipelineRunner.PipelineRunner(new StepRegistry([step]), contextFactory);

            var request = new PipelineRequest("compute", [4], ".\\generated-compute", SkipBuild: true, SkipValidation: false, DryRun: false);
            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.FatalExitCode, exitCode);
            Assert.Equal(3, step.Executions); // 1 initial + 2 retries = 3 total attempts
            Assert.Contains(reportWriter.Messages, message => message.Contains("Retry attempt", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_PostValidatorFailure_SucceedsOnRetry()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-retry-success-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var processRunner = new RecordingProcessRunner();
            var reportWriter = new BufferedReportWriter();
            var contextFactory = new PipelineContextFactory(
                processRunner,
                new WorkspaceManager(),
                new StaticCliMetadataLoader(),
                new TargetMatcher(),
                new StubFilteredCliWriter(),
                new StubBuildCoordinator(),
                new StubAiCapabilityProbe(),
                reportWriter,
                repoRoot);

            // Validator that fails first time but succeeds on second attempt
            var validator = new CountingValidator(failUntilAttempt: 2);
            var step = new RecordingStep(
                id: 4,
                name: "Generate tool-family article",
                failurePolicy: FailurePolicy.Fatal,
                success: true,
                maxRetries: 2,
                postValidators: [validator]);
            var runner = new global::PipelineRunner.PipelineRunner(new StepRegistry([step]), contextFactory);

            var request = new PipelineRequest("compute", [4], ".\\generated-compute", SkipBuild: true, SkipValidation: false, DryRun: false);
            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            Assert.Equal(2, step.Executions); // Initial attempt + 1 retry
            Assert.Equal(2, validator.Attempts);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WarnFailureContinuesToLaterSteps()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-warn-hook-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var processRunner = new RecordingProcessRunner();
            var reportWriter = new BufferedReportWriter();
            var contextFactory = new PipelineContextFactory(
                processRunner,
                new WorkspaceManager(),
                new StaticCliMetadataLoader(),
                new TargetMatcher(),
                new StubFilteredCliWriter(),
                new StubBuildCoordinator(),
                new StubAiCapabilityProbe(),
                reportWriter,
                repoRoot);

            var warnStep = new RecordingStep(
                id: 5,
                name: "Generate skills relevance",
                failurePolicy: FailurePolicy.Warn,
                success: false,
                warnings: ["skills relevance generation failed"]);
            var nextStep = new RecordingStep(
                id: 6,
                name: "Generate horizontal article",
                failurePolicy: FailurePolicy.Fatal,
                success: true);
            var runner = new global::PipelineRunner.PipelineRunner(new StepRegistry([warnStep, nextStep]), contextFactory);

            var request = new PipelineRequest("compute", [5, 6], ".\\generated-compute", SkipBuild: true, SkipValidation: false, DryRun: false);
            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            Assert.Equal(1, warnStep.Executions);
            Assert.Equal(1, nextStep.Executions);
            Assert.Contains(reportWriter.Messages, message => message.Contains("skills relevance generation failed", StringComparison.Ordinal));
            Assert.Contains(reportWriter.Messages, message => message.Contains("critical failure record", StringComparison.OrdinalIgnoreCase));

            var outputRoot = Path.GetFullPath(Path.Combine(repoRoot, "generated-compute"));
            var warnStepDirectory = Path.Combine(outputRoot, global::PipelineRunner.PipelineRunner.GetStepIdentifierSlug(warnStep));
            var nextStepDirectory = Path.Combine(outputRoot, global::PipelineRunner.PipelineRunner.GetStepIdentifierSlug(nextStep));

            Assert.True(StepResultReader.TryRead(warnStepDirectory, out var warnEnvelope));
            Assert.NotNull(warnEnvelope);
            Assert.Equal(global::PipelineRunner.PipelineRunner.GetStepIdentifierSlug(warnStep), warnEnvelope!.StepName);
            Assert.Equal(StepResultStatus.Failure, warnEnvelope.Status);

            Assert.True(StepResultReader.TryRead(nextStepDirectory, out var nextEnvelope));
            Assert.NotNull(nextEnvelope);
            Assert.Equal(global::PipelineRunner.PipelineRunner.GetStepIdentifierSlug(nextStep), nextEnvelope!.StepName);
            Assert.Equal(StepResultStatus.Success, nextEnvelope.Status);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public void BuildStepResultEnvelope_MapsRunnerResultToSharedEnvelope()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-envelope-{Guid.NewGuid():N}");
        var outputRoot = Path.Combine(repoRoot, "generated-compute");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        Directory.CreateDirectory(Path.Combine(outputRoot, "reports"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var reportWriter = new BufferedReportWriter();
            var context = new PipelineContext
            {
                Request = new PipelineRequest("compute", [4], outputRoot, SkipBuild: true, SkipValidation: false, DryRun: false),
                RepoRoot = repoRoot,
                McpToolsRoot = Path.Combine(repoRoot, "mcp-tools"),
                OutputPath = outputRoot,
                ProcessRunner = new RecordingProcessRunner(),
                Workspaces = new WorkspaceManager(),
                CliMetadataLoader = new StaticCliMetadataLoader(),
                TargetMatcher = new TargetMatcher(),
                FilteredCliWriter = new StubFilteredCliWriter(),
                BuildCoordinator = new StubBuildCoordinator(),
                AiCapabilityProbe = new StubAiCapabilityProbe(),
                Reports = reportWriter
            };
            context.Items["Namespace"] = "compute";

            var outputFile = Path.Combine(outputRoot, "reports", "summary.json");
            File.WriteAllText(outputFile, """{ "ok": true }""");

            var step = new RecordingStep(
                id: 4,
                name: "Generate tool-family article",
                failurePolicy: FailurePolicy.Fatal,
                success: false,
                postValidators: [new FixedValidator(success: false, warnings: ["validator failed"])]);
            var result = new StepResult(
                Success: false,
                Warnings: ["artifact incomplete"],
                Duration: TimeSpan.FromMilliseconds(1250),
                Outputs: [outputFile],
                ProcessInvocations: ["dotnet run --project tool-family"],
                ValidatorResults: [new ValidatorResult("FixedValidator", false, ["validator failed"])],
                ArtifactFailures:
                [
                    ArtifactFailure.Create(
                        "tool",
                        "compute list",
                        "Article assembly failed.",
                        ["Missing H2 section."])
                ]);

            var envelope = global::PipelineRunner.PipelineRunner.BuildStepResultEnvelope(context, step, result);

            Assert.Equal("1.0", envelope.SchemaVersion);
            Assert.Equal("Generate tool-family article", envelope.Step);
            Assert.Equal("step-4-generate-tool-family-article", envelope.StepName);
            Assert.Equal("compute", envelope.Namespace);
            Assert.Equal(StepResultStatus.Partial, envelope.Status);
            Assert.Equal(1, envelope.OutputFileCount);
            Assert.Equal(1250L, envelope.DurationMs);
            Assert.Equal("00:00:01.2500000", envelope.Duration);
            Assert.Equal(ValidationStatus.Failed, envelope.ValidationStatus);
            Assert.NotNull(envelope.OutputArtifacts);
            Assert.Single(envelope.OutputArtifacts!);
            Assert.Equal("reports/summary.json", envelope.OutputArtifacts[0].Path);
            Assert.NotEmpty(envelope.OutputArtifacts[0].Sha256);
            Assert.Contains("Article assembly failed.", envelope.Errors);
            Assert.Contains("validator failed", envelope.Warnings);
            Assert.True(DateTimeOffset.TryParse(envelope.Timestamp, out _));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ArtifactFailuresWriteRecordsAndSummary()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-artifact-failures-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var processRunner = new RecordingProcessRunner();
            var reportWriter = new BufferedReportWriter();
            var contextFactory = new PipelineContextFactory(
                processRunner,
                new WorkspaceManager(),
                new StaticCliMetadataLoader(),
                new TargetMatcher(),
                new StubFilteredCliWriter(),
                new StubBuildCoordinator(),
                new StubAiCapabilityProbe(),
                reportWriter,
                repoRoot);

            var step = new RecordingStep(
                id: 2,
                name: "Generate example prompts",
                failurePolicy: FailurePolicy.Fatal,
                success: true,
                artifactFailures:
                [
                    ArtifactFailure.Create(
                        "tool",
                        "compute list",
                        "Example prompt validation failed for this tool.",
                        ["Missing required parameters in one or more prompts."],
                        [Path.Combine(repoRoot, "generated-compute", "example-prompts", "azure-compute-list-example-prompts.md")])
                ]);
            var runner = new global::PipelineRunner.PipelineRunner(new StepRegistry([step]), contextFactory);

            var outputRelativePath = Path.Combine(".", "generated-compute");
            var request = new PipelineRequest("compute", [2], outputRelativePath, SkipBuild: true, SkipValidation: false, DryRun: false);
            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            var failureDirectory = Path.Combine(Path.GetFullPath(Path.Combine(repoRoot, outputRelativePath)), "critical-failures");
            var failureFiles = Directory.GetFiles(failureDirectory, "*.json");
            Assert.Single(failureFiles);
            Assert.Contains(reportWriter.Messages, message => message.Contains("Critical failures summary:", StringComparison.Ordinal));
            Assert.Contains(reportWriter.Messages, message => message.Contains("compute list", StringComparison.Ordinal));
            Assert.Contains(reportWriter.Messages, message => message.Contains("Record:", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    private sealed class RecordingStep : StepDefinition
    {
        private readonly bool _success;
        private readonly IReadOnlyList<string> _warnings;
        private readonly IReadOnlyList<ArtifactFailure> _artifactFailures;

        public RecordingStep(
            int id,
            string name,
            FailurePolicy failurePolicy,
            bool success,
            IReadOnlyList<string>? warnings = null,
            IReadOnlyList<IPostValidator>? postValidators = null,
            IReadOnlyList<ArtifactFailure>? artifactFailures = null,
            int maxRetries = 0)
            : base(id, name, StepScope.Namespace, failurePolicy, postValidators: postValidators, maxRetries: maxRetries)
        {
            _success = success;
            _warnings = warnings ?? Array.Empty<string>();
            _artifactFailures = artifactFailures ?? Array.Empty<ArtifactFailure>();
        }

        public int Executions { get; private set; }

        public override ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            Executions++;
            return ValueTask.FromResult(new StepResult(_success, _warnings, TimeSpan.Zero, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ValidatorResult>(), _artifactFailures));
        }
    }

    private sealed class FixedValidator(bool success, IReadOnlyList<string> warnings) : IPostValidator
    {
        public string Name => "FixedValidator";

        public ValueTask<ValidatorResult> ValidateAsync(PipelineContext context, IPipelineStep step, CancellationToken cancellationToken)
            => ValueTask.FromResult(new ValidatorResult(Name, success, warnings));
    }

    private sealed class CountingValidator : IPostValidator
    {
        private readonly int _failUntilAttempt;

        public CountingValidator(int failUntilAttempt)
        {
            _failUntilAttempt = failUntilAttempt;
        }

        public int Attempts { get; private set; }

        public string Name => "CountingValidator";

        public ValueTask<ValidatorResult> ValidateAsync(PipelineContext context, IPipelineStep step, CancellationToken cancellationToken)
        {
            Attempts++;
            var success = Attempts >= _failUntilAttempt;
            var warnings = success ? Array.Empty<string>() : new[] { "validator failed" };
            return ValueTask.FromResult(new ValidatorResult(Name, success, warnings));
        }
    }

    private sealed class StaticCliMetadataLoader : ICliMetadataLoader
    {
        private readonly CliMetadataSnapshot _snapshot = CreateSnapshot();

        public bool CliOutputExists(string outputPath) => true;

        public bool CliVersionExists(string outputPath) => true;

        public bool NamespaceMetadataExists(string outputPath) => true;

        public ValueTask<CliMetadataSnapshot> LoadCliOutputAsync(string outputPath, CancellationToken cancellationToken)
            => ValueTask.FromResult(_snapshot);

        public ValueTask<string> LoadCliVersionAsync(string outputPath, CancellationToken cancellationToken)
            => ValueTask.FromResult("1.2.3");

        public ValueTask<IReadOnlyList<string>> LoadNamespacesAsync(string outputPath, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<string>>(["compute"]);

        private static CliMetadataSnapshot CreateSnapshot()
        {
            var json = JsonSerializer.Serialize(new
            {
                version = "1.2.3",
                results = new[]
                {
                    new
                    {
                        command = "compute list",
                        name = "compute list",
                        description = "desc",
                    },
                },
            });

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement.Clone();
            var tool = root.GetProperty("results")[0];
            return new CliMetadataSnapshot(
                Path.Combine(Path.GetTempPath(), $"cli-output-{Guid.NewGuid():N}.json"),
                root,
                [new CliTool(
                    tool.GetProperty("command").GetString() ?? string.Empty,
                    tool.GetProperty("name").GetString() ?? string.Empty,
                    tool.GetProperty("description").GetString(),
                    tool.Clone())]);
        }
    }
}
