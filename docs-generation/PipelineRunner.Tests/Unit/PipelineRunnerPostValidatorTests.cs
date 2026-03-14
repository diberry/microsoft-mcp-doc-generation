using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class PipelineRunnerPostValidatorTests
{
    [Fact]
    public async Task RunAsync_PostValidatorFailureStopsPipeline()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-validator-hook-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs-generation", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "docs-generation.sln"), string.Empty);

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
    public async Task RunAsync_WarnFailureContinuesToLaterSteps()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-warn-hook-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "docs-generation", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "docs-generation.sln"), string.Empty);

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
            Assert.Contains(reportWriter.Messages, message => message.Contains("Pipeline completed with 1 warning(s).", StringComparison.Ordinal));
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

        public RecordingStep(
            int id,
            string name,
            FailurePolicy failurePolicy,
            bool success,
            IReadOnlyList<string>? warnings = null,
            IReadOnlyList<IPostValidator>? postValidators = null)
            : base(id, name, StepScope.Namespace, failurePolicy, postValidators: postValidators)
        {
            _success = success;
            _warnings = warnings ?? Array.Empty<string>();
        }

        public int Executions { get; private set; }

        public override ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            Executions++;
            return ValueTask.FromResult(new StepResult(_success, _warnings, TimeSpan.Zero, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ValidatorResult>()));
        }
    }

    private sealed class FixedValidator(bool success, IReadOnlyList<string> warnings) : IPostValidator
    {
        public string Name => "FixedValidator";

        public ValueTask<ValidatorResult> ValidateAsync(PipelineContext context, IPipelineStep step, CancellationToken cancellationToken)
            => ValueTask.FromResult(new ValidatorResult(Name, success, warnings));
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
