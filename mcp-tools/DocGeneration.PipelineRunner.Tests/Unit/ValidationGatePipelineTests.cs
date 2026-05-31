using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Pipeline-level tests ensuring fingerprint and prompt regression gates
/// integrate correctly with PipelineRunner.RunAsync().
/// </summary>
public class ValidationGatePipelineTests
{
    // ── Fingerprint gate failure stops the pipeline ─────────────────────────

    [Fact]
    public async Task RunAsync_FingerprintGateFails_ReturnsFatalExitCode()
    {
        var repoRoot = CreateTestRoot();
        try
        {
            var runner = BuildRunner(repoRoot,
                fingerprintGate: new StubFingerprintGate(success: false, reason: "quality regressions detected"),
                promptRegressionGate: null);

            var request = new PipelineRequest(null, [1], ".\\generated",
                SkipBuild: true, SkipValidation: false, DryRun: false,
                SkipChangelogGate: true,
                RunFingerprintGate: true,
                RunPromptRegressionGate: false);

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.FatalExitCode, exitCode);
        }
        finally { Directory.Delete(repoRoot, true); }
    }

    // ── Prompt regression gate failure stops the pipeline ──────────────────

    [Fact]
    public async Task RunAsync_PromptRegressionGateFails_ReturnsFatalExitCode()
    {
        var repoRoot = CreateTestRoot();
        try
        {
            var runner = BuildRunner(repoRoot,
                fingerprintGate: null,
                promptRegressionGate: new StubPromptRegressionGate(success: false, reason: "2 tests failed"));

            var request = new PipelineRequest(null, [1], ".\\generated",
                SkipBuild: true, SkipValidation: false, DryRun: false,
                SkipChangelogGate: true,
                RunFingerprintGate: false,
                RunPromptRegressionGate: true);

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.FatalExitCode, exitCode);
        }
        finally { Directory.Delete(repoRoot, true); }
    }

    // ── Gates disabled by default — not invoked ─────────────────────────────

    [Fact]
    public async Task RunAsync_FingerprintGateFlagFalse_GateNotInvoked()
    {
        var repoRoot = CreateTestRoot();
        try
        {
            var fpGate = new StubFingerprintGate(success: false, reason: "should not be called");
            var runner = BuildRunner(repoRoot, fingerprintGate: fpGate, promptRegressionGate: null);

            // RunFingerprintGate defaults to false
            var request = new PipelineRequest(null, [1], ".\\generated",
                SkipBuild: true, SkipValidation: false, DryRun: false,
                SkipChangelogGate: true);

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            Assert.Equal(0, fpGate.CallCount); // gate was never called
        }
        finally { Directory.Delete(repoRoot, true); }
    }

    [Fact]
    public async Task RunAsync_PromptRegressionGateFlagFalse_GateNotInvoked()
    {
        var repoRoot = CreateTestRoot();
        try
        {
            var prgGate = new StubPromptRegressionGate(success: false, reason: "should not be called");
            var runner = BuildRunner(repoRoot, fingerprintGate: null, promptRegressionGate: prgGate);

            // RunPromptRegressionGate defaults to false
            var request = new PipelineRequest(null, [1], ".\\generated",
                SkipBuild: true, SkipValidation: false, DryRun: false,
                SkipChangelogGate: true);

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            Assert.Equal(0, prgGate.CallCount);
        }
        finally { Directory.Delete(repoRoot, true); }
    }

    // ── Both gates pass — pipeline succeeds ────────────────────────────────

    [Fact]
    public async Task RunAsync_BothGatesPass_ReturnsSuccess()
    {
        var repoRoot = CreateTestRoot();
        try
        {
            var runner = BuildRunner(repoRoot,
                fingerprintGate: new StubFingerprintGate(success: true, reason: "no regressions"),
                promptRegressionGate: new StubPromptRegressionGate(success: true, reason: "all tests pass"));

            var request = new PipelineRequest(null, [1], ".\\generated",
                SkipBuild: true, SkipValidation: false, DryRun: false,
                SkipChangelogGate: true,
                RunFingerprintGate: true,
                RunPromptRegressionGate: true);

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
        }
        finally { Directory.Delete(repoRoot, true); }
    }

    // ── Gates run after namespace steps complete ───────────────────────────

    [Fact]
    public async Task RunAsync_GatesRunAfterNamespaceStepsComplete()
    {
        var repoRoot = CreateTestRoot();
        try
        {
            var executionOrder = new List<string>();

            var step = new OrderTrackingStep(1, "Generate annotations", StepScope.Namespace, executionOrder);
            var fpGate = new OrderTrackingFingerprintGate(executionOrder);
            var runner = BuildRunnerWithStep(repoRoot, step, fpGate, null);

            var request = new PipelineRequest(null, [1], ".\\generated",
                SkipBuild: true, SkipValidation: false, DryRun: false,
                SkipChangelogGate: true,
                RunFingerprintGate: true,
                RunPromptRegressionGate: false);

            await runner.RunAsync(request, CancellationToken.None);

            // All namespace steps come before the gate
            var lastStepIndex = executionOrder.LastIndexOf(
                executionOrder.FindLast(e => e.StartsWith("step:", StringComparison.Ordinal)) ?? "");
            var gateIndex = executionOrder.IndexOf("fingerprint-gate");

            Assert.True(gateIndex > lastStepIndex,
                $"Fingerprint gate should run after steps. Order: [{string.Join(", ", executionOrder)}]");
        }
        finally { Directory.Delete(repoRoot, true); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string CreateTestRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"vg-pipeline-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(dir, "mcp-doc-generation.sln"), string.Empty);
        return dir;
    }

    private static global::PipelineRunner.PipelineRunner BuildRunner(
        string repoRoot,
        IFingerprintGate? fingerprintGate,
        IPromptRegressionGate? promptRegressionGate)
    {
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

        var noopStep = new NoopNamespaceStep();
        return new global::PipelineRunner.PipelineRunner(
            new StepRegistry([noopStep]),
            contextFactory,
            changelogGate: null,
            fingerprintGate: fingerprintGate,
            promptRegressionGate: promptRegressionGate);
    }

    private static global::PipelineRunner.PipelineRunner BuildRunnerWithStep(
        string repoRoot,
        IPipelineStep step,
        IFingerprintGate? fingerprintGate,
        IPromptRegressionGate? promptRegressionGate)
    {
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

        return new global::PipelineRunner.PipelineRunner(
            new StepRegistry([step]),
            contextFactory,
            changelogGate: null,
            fingerprintGate: fingerprintGate,
            promptRegressionGate: promptRegressionGate);
    }

    // ── Stub gates ────────────────────────────────────────────────────────

    private sealed class StubFingerprintGate(bool success, string reason) : IFingerprintGate
    {
        public int CallCount { get; private set; }

        public Task<FingerprintGateResult> EvaluateAsync(string repoRoot, string mcpToolsRoot, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(success
                ? FingerprintGateResult.Pass(reason)
                : FingerprintGateResult.Fail(reason));
        }
    }

    private sealed class StubPromptRegressionGate(bool success, string reason) : IPromptRegressionGate
    {
        public int CallCount { get; private set; }

        public Task<PromptRegressionGateResult> EvaluateAsync(string mcpToolsRoot, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(success
                ? PromptRegressionGateResult.Pass(reason)
                : PromptRegressionGateResult.Fail(reason));
        }
    }

    private sealed class OrderTrackingFingerprintGate(List<string> order) : IFingerprintGate
    {
        public Task<FingerprintGateResult> EvaluateAsync(string repoRoot, string mcpToolsRoot, CancellationToken ct)
        {
            order.Add("fingerprint-gate");
            return Task.FromResult(FingerprintGateResult.Pass("ok"));
        }
    }

    // ── Test step helpers ──────────────────────────────────────────────────

    private sealed class NoopNamespaceStep : StepDefinition
    {
        public NoopNamespaceStep() : base(1, "Noop step", StepScope.Namespace, FailurePolicy.Fatal) { }

        public override ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken ct)
            => ValueTask.FromResult(new StepResult(true, Array.Empty<string>(), TimeSpan.Zero,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ValidatorResult>(),
                Array.Empty<ArtifactFailure>()));
    }

    private sealed class OrderTrackingStep : StepDefinition
    {
        private readonly List<string> _order;

        public OrderTrackingStep(int id, string name, StepScope scope, List<string> order)
            : base(id, name, scope, FailurePolicy.Fatal)
        {
            _order = order;
        }

        public override ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken ct)
        {
            _order.Add($"step:{Id}");
            return ValueTask.FromResult(new StepResult(true, Array.Empty<string>(), TimeSpan.Zero,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ValidatorResult>(),
                Array.Empty<ArtifactFailure>()));
        }
    }

    // ── CLI metadata loader that returns two namespaces ────────────────────

    private sealed class StaticCliMetadataLoader : ICliMetadataLoader
    {
        private readonly CliMetadataSnapshot _snapshot = CreateSnapshot();

        public bool CliOutputExists(string outputPath) => true;
        public bool CliVersionExists(string outputPath) => true;
        public bool NamespaceMetadataExists(string outputPath) => true;

        public ValueTask<CliMetadataSnapshot> LoadCliOutputAsync(string outputPath, CancellationToken ct)
            => ValueTask.FromResult(_snapshot);

        public ValueTask<string> LoadCliVersionAsync(string outputPath, CancellationToken ct)
            => ValueTask.FromResult("1.0.0");

        public ValueTask<IReadOnlyList<string>> LoadNamespacesAsync(string outputPath, CancellationToken ct)
            => ValueTask.FromResult<IReadOnlyList<string>>(["compute", "storage"]);

        private static CliMetadataSnapshot CreateSnapshot()
        {
            var json = JsonSerializer.Serialize(new
            {
                results = new[]
                {
                    new { command = "compute list", name = "compute list", description = "desc" },
                }
            });
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement.Clone();
            var tools = root.GetProperty("results")
                .EnumerateArray()
                .Select(t => new CliTool(
                    t.GetProperty("command").GetString() ?? string.Empty,
                    t.GetProperty("name").GetString() ?? string.Empty,
                    t.GetProperty("description").GetString(),
                    t.Clone()))
                .ToArray();
            return new CliMetadataSnapshot(
                Path.Combine(Path.GetTempPath(), $"cli-{Guid.NewGuid():N}.json"), root, tools);
        }
    }
}
