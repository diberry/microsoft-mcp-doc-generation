using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Shared;
using Shared.Validation;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Verifies PipelineRunner pre-AI validation gate behavior.
/// </summary>
public sealed class PreAiValidationGateTests
{
    [Fact]
    public async Task RunAsync_PreAiGate_DoesNotFireWhenNoReducerRegistered()
    {
        var (repoRoot, contextFactory) = CreateTestEnv("preai-no-reducer");

        try
        {
            var preAiRegistry = new ReducerRegistry();
            preAiRegistry.RegisterValidator(new AlwaysFailValidator<StubContext>("should not fire"));

            var step = new RecordingStep(id: 4, name: "test-step", failurePolicy: FailurePolicy.Fatal, success: true);
            var runner = new global::PipelineRunner.PipelineRunner(
                new StepRegistry([step]),
                contextFactory,
                preAiRegistry: preAiRegistry);

            var exitCode = await runner.RunAsync(
                new PipelineRequest("compute", [4], ".\\generated-compute", SkipBuild: true, SkipValidation: false, DryRun: false),
                CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            Assert.Equal(1, step.Executions);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_PreAiGate_SkipsStepOnValidatorFailure()
    {
        var (repoRoot, contextFactory) = CreateTestEnv("preai-skip-step");

        try
        {
            var preAiRegistry = new ReducerRegistry();
            preAiRegistry.Register(4, static (ctx, ct) => Task.FromResult<object>(new StubContext("value")));
            preAiRegistry.RegisterValidator(new AlwaysFailValidator<StubContext>("pre-ai blocked"));

            var step = new RecordingStep(id: 4, name: "test-step", failurePolicy: FailurePolicy.Fatal, success: true);
            var runner = new global::PipelineRunner.PipelineRunner(
                new StepRegistry([step]),
                contextFactory,
                preAiRegistry: preAiRegistry);

            var exitCode = await runner.RunAsync(
                new PipelineRequest("compute", [4], ".\\generated-compute", SkipBuild: true, SkipValidation: false, DryRun: false),
                CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            Assert.Equal(0, step.Executions);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_PreAiGate_RecordsValidationStatusFailed()
    {
        var (repoRoot, contextFactory) = CreateTestEnv("preai-status");
        var outputPath = Path.Combine(repoRoot, "generated-compute");

        try
        {
            var preAiRegistry = new ReducerRegistry();
            preAiRegistry.Register(4, static (ctx, ct) => Task.FromResult<object>(new StubContext("value")));
            preAiRegistry.RegisterValidator(new AlwaysFailValidator<StubContext>("budget exceeded"));

            var step = new RecordingStep(id: 4, name: "generate-tool-family-article", failurePolicy: FailurePolicy.Fatal, success: true);
            var runner = new global::PipelineRunner.PipelineRunner(
                new StepRegistry([step]),
                contextFactory,
                preAiRegistry: preAiRegistry);

            var exitCode = await runner.RunAsync(
                new PipelineRequest("compute", [4], ".\\generated-compute", SkipBuild: true, SkipValidation: false, DryRun: false),
                CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);

            var stepSlug = global::PipelineRunner.PipelineRunner.GetStepIdentifierSlug(step);
            var stepWorkspaceDir = Path.Combine(outputPath, stepSlug);
            Assert.True(StepResultReader.TryRead(stepWorkspaceDir, out var envelope),
                $"Step result envelope not found at: {stepWorkspaceDir}");
            Assert.NotNull(envelope);
            Assert.Equal(ValidationStatus.Failed, envelope!.ValidationStatus);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_PreAiGate_Failure_IsNonFatal_PipelineContinues()
    {
        var (repoRoot, contextFactory) = CreateTestEnv("preai-nonfatal");

        try
        {
            var preAiRegistry = new ReducerRegistry();
            preAiRegistry.Register(4, static (ctx, ct) => Task.FromResult<object>(new StubContext("value")));
            preAiRegistry.RegisterValidator(new AlwaysFailValidator<StubContext>("blocked"));

            var step4 = new RecordingStep(id: 4, name: "step-four", failurePolicy: FailurePolicy.Fatal, success: true);
            var step6 = new RecordingStep(id: 6, name: "step-six", failurePolicy: FailurePolicy.Fatal, success: true);
            var runner = new global::PipelineRunner.PipelineRunner(
                new StepRegistry([step4, step6]),
                contextFactory,
                preAiRegistry: preAiRegistry);

            var exitCode = await runner.RunAsync(
                new PipelineRequest("compute", [4, 6], ".\\generated-compute", SkipBuild: true, SkipValidation: false, DryRun: false),
                CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            Assert.Equal(0, step4.Executions);
            Assert.Equal(1, step6.Executions);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_PreAiGate_DoesNotFireWhenSkipValidationIsTrue()
    {
        var (repoRoot, contextFactory) = CreateTestEnv("preai-skip-validation");

        try
        {
            var preAiRegistry = new ReducerRegistry();
            preAiRegistry.Register(4, static (ctx, ct) => Task.FromResult<object>(new StubContext("value")));
            preAiRegistry.RegisterValidator(new AlwaysFailValidator<StubContext>("should be skipped"));

            var step = new RecordingStep(id: 4, name: "test-step", failurePolicy: FailurePolicy.Fatal, success: true);
            var runner = new global::PipelineRunner.PipelineRunner(
                new StepRegistry([step]),
                contextFactory,
                preAiRegistry: preAiRegistry);

            var exitCode = await runner.RunAsync(
                new PipelineRequest("compute", [4], ".\\generated-compute", SkipBuild: true, SkipValidation: true, DryRun: false),
                CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            Assert.Equal(1, step.Executions);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    private static (string repoRoot, PipelineContextFactory contextFactory) CreateTestEnv(string suffix)
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"preai-gate-{suffix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        var contextFactory = new PipelineContextFactory(
            new RecordingProcessRunner(),
            new WorkspaceManager(),
            new StaticCliMetadataLoader(),
            new TargetMatcher(),
            new StubFilteredCliWriter(),
            new StubBuildCoordinator(),
            new StubAiCapabilityProbe(),
            new BufferedReportWriter(),
            repoRoot);

        return (repoRoot, contextFactory);
    }

    private sealed record StubContext(string Value);

    private sealed class AlwaysFailValidator<TContext>(string message) : IPreAiValidator<TContext>
    {
        public Type ContextType => typeof(TContext);

        public Task<PreAiValidationResult> ValidateAsync(TContext context, CancellationToken cancellationToken)
            => Task.FromResult(PreAiValidationResult.Fail(new ValidationError("field", message, ValidationSeverity.Error)));

        public Task<PreAiValidationResult> ValidateAsync(object context, CancellationToken cancellationToken)
            => ValidateAsync((TContext)context, cancellationToken);
    }

    private sealed class RecordingStep : StepDefinition
    {
        private readonly bool _success;
        private int _executions;

        public int Executions => _executions;

        public RecordingStep(int id, string name, FailurePolicy failurePolicy, bool success)
            : base(id, name, StepScope.Namespace, failurePolicy)
        {
            _success = success;
        }

        public override ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _executions);
            return ValueTask.FromResult(new StepResult(
                Success: _success,
                Warnings: [],
                Duration: TimeSpan.Zero,
                Outputs: [],
                ProcessInvocations: [],
                ValidatorResults: [],
                ArtifactFailures: []));
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
                [new CliTool(tool.GetProperty("command").GetString() ?? string.Empty, tool.GetProperty("name").GetString() ?? string.Empty, tool.GetProperty("description").GetString(), tool.Clone())]);
        }
    }
}
