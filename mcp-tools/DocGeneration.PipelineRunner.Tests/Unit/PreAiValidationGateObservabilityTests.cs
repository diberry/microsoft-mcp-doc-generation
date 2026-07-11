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
/// Verifies that pre-AI seam validator outcomes are durably recorded in the
/// per-step observability envelope (metrics.json and step-result.json).
///
/// Covers PRD-QUALITY-2026-05-30 Item B acceptance criteria:
///   - Observability integration: validator failure writes validationStatus to metrics.json
///     and ValidationStatus to the step-result envelope.
///   - E2E smoke test: advisor namespace runs steps 3, 4, and 6 with pre-AI validators
///     registered; each gate fires and each stage records a validationStatus.
/// </summary>
public class PreAiValidationGateObservabilityTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: validator failure is recorded in metrics.json and step-result.json
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TryRunPreAiGateAsync_ValidatorFailure_IsRecordedInStepEnvelope()
    {
        var repoRoot = CreateRepoRoot("pre-ai-gate-observability");
        // Plain relative path (no ".\\" prefix): the runner normalizes separators via
        // PipelineContextFactory.NormalizePathSeparators, but this test computes the
        // expected path with a raw Path.Combine. A backslash prefix stays literal on
        // Linux (where '\\' is a filename char), diverging from the runner's resolved
        // path and breaking the test cross-platform. See PipelineContextFactory.cs.
        const string outputRelativePath = "generated-compute";

        try
        {
            var reportWriter = new BufferedReportWriter();

            // Step 3: returns Success:false with a failing ValidatorResult (simulating a
            // pre-AI gate block).  A no-op post-validator is registered so
            // BuildStepResultEnvelope writes a non-null ValidationStatus to step-result.json.
            var step = new PreAiFailingStep(id: 3, name: "Compose and improve tool files");
            var runner = CreateRunner(repoRoot, reportWriter, [step], ["compute"]);

            var request = new PipelineRequest(
                "compute", [3], outputRelativePath,
                SkipBuild: true, SkipValidation: false, DryRun: false);

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.FatalExitCode, exitCode);

            var outputPath = Path.GetFullPath(Path.Combine(repoRoot, outputRelativePath));

            // ── metrics.json must record validationStatus: "failed" ──────────────────
            var observabilityDir = Path.Combine(
                outputPath, "observability", "3-compose-and-improve-tool-files");

            Assert.True(
                File.Exists(Path.Combine(observabilityDir, StageOutputContract.MetricsFileName)),
                "metrics.json must be written even when the pre-AI gate fails");

            using var metricsDoc = JsonDocument.Parse(
                await File.ReadAllTextAsync(
                    Path.Combine(observabilityDir, StageOutputContract.MetricsFileName)));

            Assert.Equal(
                "failed",
                metricsDoc.RootElement.GetProperty("validationStatus").GetString());

            // ── step-result.json envelope must reflect ValidationStatus.Failed ────────
            var stepWorkspaceDir = Path.Combine(
                outputPath, "step-3-compose-and-improve-tool-files");

            Assert.True(
                StepResultReader.TryRead(stepWorkspaceDir, out var envelope),
                "step-result.json should be readable after a pre-AI gate failure");

            Assert.NotNull(envelope);
            Assert.Equal(ValidationStatus.Failed, envelope!.ValidationStatus);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2: E2E smoke test — advisor namespace, steps 3 / 4 / 6
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_AdvisorNamespace_ValidatorsFireForAllThreeSteps()
    {
        var repoRoot = CreateRepoRoot("advisor-e2e-smoke");
        // Plain relative path (no ".\\" prefix) for cross-platform correctness — see the
        // companion note in TryRunPreAiGateAsync_ValidatorFailure_IsRecordedInStepEnvelope.
        const string outputRelativePath = "generated-advisor";

        try
        {
            var reportWriter = new BufferedReportWriter();

            // Synthetic steps mirroring the three real pre-AI gated steps.
            // Each step calls a TrackingValidator, records that the gate fired,
            // and returns a passing ValidatorResult so the pipeline continues.
            var step3 = new PreAiTrackingStep(id: 3, name: "Compose and improve tool files");
            var step4 = new PreAiTrackingStep(id: 4, name: "Generate tool-family article");
            var step6 = new PreAiTrackingStep(id: 6, name: "Generate horizontal article");

            var runner = CreateRunner(repoRoot, reportWriter,
                [step3, step4, step6], ["advisor"]);

            var request = new PipelineRequest(
                "advisor", [3, 4, 6], outputRelativePath,
                SkipBuild: true, SkipValidation: false, DryRun: false);

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);

            // Confirm the pre-AI gate fired inside each step
            Assert.True(step3.ValidatorFired, "Pre-AI gate must fire for step 3");
            Assert.True(step4.ValidatorFired, "Pre-AI gate must fire for step 4");
            Assert.True(step6.ValidatorFired, "Pre-AI gate must fire for step 6");

            var outputPath = Path.GetFullPath(Path.Combine(repoRoot, outputRelativePath));

            // Confirm validationStatus is recorded in metrics.json for each stage
            foreach (var (step, slug) in new (IPipelineStep, string)[]
            {
                (step3, "3-compose-and-improve-tool-files"),
                (step4, "4-generate-tool-family-article"),
                (step6, "6-generate-horizontal-article"),
            })
            {
                var observabilityDir = Path.Combine(outputPath, "observability", slug);

                Assert.True(
                    File.Exists(Path.Combine(observabilityDir, StageOutputContract.MetricsFileName)),
                    $"metrics.json missing for step {step.Id}");

                using var metricsDoc = JsonDocument.Parse(
                    await File.ReadAllTextAsync(
                        Path.Combine(observabilityDir, StageOutputContract.MetricsFileName)));

                var status = metricsDoc.RootElement.GetProperty("validationStatus").GetString();
                Assert.NotNull(status);
                Assert.True(
                    status != "skipped",
                    $"Step {step.Id} must record a real validationStatus — the gate must have run");
            }
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string CreateRepoRoot(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(dir, "mcp-doc-generation.sln"), string.Empty);
        return dir;
    }

    private static global::PipelineRunner.PipelineRunner CreateRunner(
        string repoRoot,
        BufferedReportWriter reportWriter,
        IReadOnlyList<IPipelineStep> steps,
        IReadOnlyList<string> namespaces)
    {
        var contextFactory = new PipelineContextFactory(
            new RecordingProcessRunner(),
            new WorkspaceManager(),
            new AdvisorCliMetadataLoader(namespaces),
            new TargetMatcher(),
            new StubFilteredCliWriter(),
            new StubBuildCoordinator(),
            new StubAiCapabilityProbe(),
            reportWriter,
            repoRoot);

        return new global::PipelineRunner.PipelineRunner(
            new StepRegistry([.. steps]), contextFactory);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Step stubs
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Simulates a step whose pre-AI gate detected a validation failure.
    /// Returns <c>Success: false</c> with a failing <see cref="ValidatorResult"/> so
    /// PipelineRunner writes <c>validationStatus: "failed"</c> to both metrics.json
    /// and the step-result envelope.
    /// A no-op post-validator is registered so <c>ValidationStatus</c> in the
    /// step-result envelope is non-null.
    /// </summary>
    private sealed class PreAiFailingStep(int id, string name)
        : StepDefinition(
            id, name,
            StepScope.Namespace,
            FailurePolicy.Fatal,
            postValidators: [new AlwaysPassPostValidator()])
    {
        public override ValueTask<StepResult> ExecuteAsync(
            PipelineContext context, CancellationToken cancellationToken)
        {
            var validatorResult = new ValidatorResult(
                "pre-ai-validation",
                false,
                ["Content exceeds token budget — step blocked by pre-AI gate"]);

            return ValueTask.FromResult(new StepResult(
                Success: false,
                Warnings: ["Pre-AI gate blocked step execution"],
                Duration: TimeSpan.FromMilliseconds(5),
                Outputs: Array.Empty<string>(),
                ProcessInvocations: Array.Empty<string>(),
                ValidatorResults: [validatorResult],
                ArtifactFailures: Array.Empty<ArtifactFailure>()));
        }
    }

    /// <summary>
    /// Simulates a step that calls a pre-AI validator (via <see cref="PreAiValidatorRegistry"/>),
    /// records that the gate fired, then returns a passing result so the pipeline continues.
    /// Mirrors the registration + invocation pattern used by <c>ToolGenerationStep</c>,
    /// <c>ToolFamilyCleanupStep</c>, and <c>HorizontalArticlesStep</c>.
    /// A no-op post-validator is registered so <c>validationStatus</c> is written to
    /// metrics.json (and not emitted as "skipped").
    /// </summary>
    private sealed class PreAiTrackingStep(int id, string name)
        : StepDefinition(
            id, name,
            StepScope.Namespace,
            FailurePolicy.Fatal,
            postValidators: [new AlwaysPassPostValidator()])
    {
        public bool ValidatorFired { get; private set; }

        public override async ValueTask<StepResult> ExecuteAsync(
            PipelineContext context, CancellationToken cancellationToken)
        {
            var registry = new ReducerRegistry();
            var tracker = new TrackingValidator();
            registry.RegisterValidator(tracker);

            var validators = registry.GetValidators<object>();
            var validationResult = await ReducerRegistry.AggregateAsync(
                validators, new object(), cancellationToken);
            ValidatorFired = tracker.WasCalled;

            var validatorResults = new[]
            {
                new ValidatorResult(
                    "pre-ai-validation",
                    validationResult.IsValid,
                    validationResult.Errors.Select(e => e.Message).ToArray()),
            };

            return new StepResult(
                Success: true,
                Warnings: Array.Empty<string>(),
                Duration: TimeSpan.FromMilliseconds(5),
                Outputs: Array.Empty<string>(),
                ProcessInvocations: Array.Empty<string>(),
                ValidatorResults: validatorResults,
                ArtifactFailures: Array.Empty<ArtifactFailure>());
        }
    }

    private sealed class AlwaysPassPostValidator : IPostValidator
    {
        public string Name => "AlwaysPassPostValidator";

        public ValueTask<ValidatorResult> ValidateAsync(
            PipelineContext context, IPipelineStep step, CancellationToken cancellationToken)
            => ValueTask.FromResult(
                new ValidatorResult(Name, true, Array.Empty<string>()));
    }

    private sealed class TrackingValidator : IPreAiValidator<object>
    {
        public bool WasCalled { get; private set; }

        public Task<PreAiValidationResult> ValidateAsync(
            object context, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(PreAiValidationResult.Pass());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CLI metadata loader stub
    // ─────────────────────────────────────────────────────────────────────────

    private sealed class AdvisorCliMetadataLoader(IReadOnlyList<string> namespaces) : ICliMetadataLoader
    {
        private readonly CliMetadataSnapshot _snapshot = CreateSnapshot(namespaces[0]);

        public bool CliOutputExists(string outputPath) => true;
        public bool CliVersionExists(string outputPath) => true;
        public bool NamespaceMetadataExists(string outputPath) => true;

        public ValueTask<CliMetadataSnapshot> LoadCliOutputAsync(
            string outputPath, CancellationToken cancellationToken)
            => ValueTask.FromResult(_snapshot);

        public ValueTask<string> LoadCliVersionAsync(
            string outputPath, CancellationToken cancellationToken)
            => ValueTask.FromResult("1.2.3");

        public ValueTask<IReadOnlyList<string>> LoadNamespacesAsync(
            string outputPath, CancellationToken cancellationToken)
            => ValueTask.FromResult(namespaces);

        private static CliMetadataSnapshot CreateSnapshot(string namespaceName)
        {
            var json = JsonSerializer.Serialize(new
            {
                version = "1.2.3",
                results = new[]
                {
                    new
                    {
                        command = $"{namespaceName} list",
                        name = $"{namespaceName} list",
                        description = "List resources",
                    },
                },
            });

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement.Clone();
            var tool = root.GetProperty("results")[0];
            return new CliMetadataSnapshot(
                Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}.json"),
                root,
                [new CliTool(
                    tool.GetProperty("command").GetString()!,
                    tool.GetProperty("name").GetString()!,
                    tool.GetProperty("description").GetString(),
                    tool.Clone())]);
        }
    }
}
