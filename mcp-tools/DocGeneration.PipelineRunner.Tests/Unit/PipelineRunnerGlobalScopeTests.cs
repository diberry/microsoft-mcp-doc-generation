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

public class PipelineRunnerGlobalScopeTests
{
    [Fact]
    public async Task RunAsync_GlobalStepRunsOnceBeforeNamespaceSteps()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-global-scope-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var executionOrder = new List<string>();
            var processRunner = new RecordingProcessRunner();
            var contextFactory = new PipelineContextFactory(
                processRunner,
                new WorkspaceManager(),
                new StaticCliMetadataLoader(),
                new TargetMatcher(),
                new StubFilteredCliWriter(),
                new StubBuildCoordinator(),
                new StubAiCapabilityProbe(),
                new BufferedReportWriter(),
                repoRoot);

            var bootstrapStep = new RecordingStep(0, "Bootstrap pipeline", StepScope.Global, executionOrder);
            var namespaceStep = new RecordingStep(1, "Generate annotations", StepScope.Namespace, executionOrder);
            var runner = new global::PipelineRunner.PipelineRunner(new StepRegistry([bootstrapStep, namespaceStep]), contextFactory);

            var request = new PipelineRequest(null, [1], ".\\generated", SkipBuild: true, SkipValidation: false, DryRun: false, SkipChangelogGate: true);
            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            Assert.Equal(1, bootstrapStep.Executions);
            Assert.Equal(2, namespaceStep.Executions);
            Assert.Equal(new[] { "global", "namespace:compute", "namespace:storage" }, executionOrder);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ChangelogGateSkipsNamespace_PipelineCompletesGracefully()
    {
        // Regression guard for Bootstrap/cli-tab-config.json interaction (RISK 2):
        // Bootstrap (commit c0a6ec9) writes cli-tab-config.json listing ALL resolved namespaces BEFORE the
        // namespace loop runs. The CHANGELOG gate can then skip some of those namespaces inside the loop.
        // This test documents and verifies that skipping a namespace via the gate is graceful:
        // the pipeline succeeds and the skipped namespace's steps are not executed, even though
        // cli-tab-config.json still has an entry for it (orphan entries do not cause a pipeline error).

        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-changelog-gate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var executionOrder = new List<string>();
            var reportWriter = new BufferedReportWriter();
            var contextFactory = new PipelineContextFactory(
                new RecordingProcessRunner(),
                new WorkspaceManager(),
                new StaticCliMetadataLoader(),   // returns ["compute", "storage"]
                new TargetMatcher(),
                new StubFilteredCliWriter(),
                new StubBuildCoordinator(),
                new StubAiCapabilityProbe(),
                reportWriter,
                repoRoot);

            // Gate skips "storage" — simulating that no CHANGELOG entries exist for it.
            // In a real run Bootstrap has already written cli-tab-config.json with both namespaces,
            // but the skipped namespace never reaches Step 1+.
            var changelogGate = new SkipSpecificNamespaceGate("storage");
            var bootstrapStep = new RecordingStep(0, "Bootstrap pipeline", StepScope.Global, executionOrder);
            var namespaceStep = new RecordingStep(1, "Generate annotations", StepScope.Namespace, executionOrder);
            var runner = new global::PipelineRunner.PipelineRunner(
                new StepRegistry([bootstrapStep, namespaceStep]),
                contextFactory,
                changelogGate);

            // SkipChangelogGate = false — the gate IS active for this test
            var request = new PipelineRequest(null, [1], ".\\generated", SkipBuild: true, SkipValidation: false, DryRun: false, SkipChangelogGate: false);
            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            // Pipeline must complete successfully — skipping a namespace via the gate is not an error
            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);

            // Bootstrap (global step) runs once; namespace step runs only for "compute" (not "storage")
            Assert.Equal(1, bootstrapStep.Executions);
            Assert.Equal(1, namespaceStep.Executions);
            Assert.Equal(new[] { "global", "namespace:compute" }, executionOrder);

            // The skip reason for "storage" must appear in the pipeline report
            Assert.Contains(reportWriter.Messages,
                m => m.Contains("storage", StringComparison.OrdinalIgnoreCase)
                  && m.Contains("Skipped", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ReplayMode_UsesReplayWorkspaceAndExecutesOnlyTargetStep()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-replay-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var replayOutput = Path.Combine(repoRoot, "runs", "run-123");
            CreateReplayMetadata(replayOutput, ["compute"]);
            WriteStepEnvelope(replayOutput, 1, "generate-annotations-parameters-and-raw-tools");
            WriteStepEnvelope(replayOutput, 2, "generate-example-prompts");

            var replayStep = new ReplayRecordingStep();
            var runner = new global::PipelineRunner.PipelineRunner(
                new StepRegistry([
                    new ReplayDependencyStep(1, "Generate annotations parameters and raw tools"),
                    new ReplayDependencyStep(2, "Generate example prompts"),
                    replayStep,
                ]),
                new PipelineContextFactory(
                    new RecordingProcessRunner(),
                    new WorkspaceManager(),
                    new CliMetadataLoader(),
                    new TargetMatcher(),
                    new StubFilteredCliWriter(),
                    new StubBuildCoordinator(),
                    new StubAiCapabilityProbe(),
                    new BufferedReportWriter(),
                    repoRoot));

            var request = new PipelineRequest(
                "compute",
                [],
                ".\\generated",
                SkipBuild: true,
                SkipValidation: false,
                DryRun: false,
                Replay: true,
                ReplayFromRunId: "run-123",
                ReplayStepName: "compose-and-improve-tool-files");

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            Assert.Equal(1, replayStep.Executions);
            Assert.Equal(Path.GetFullPath(replayOutput), replayStep.LastOutputPath);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ReplayMode_MissingUpstreamEnvelope_ReturnsFatalExitCode()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-replay-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        var originalError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);

        try
        {
            var replayOutput = Path.Combine(repoRoot, "runs", "run-123");
            CreateReplayMetadata(replayOutput, ["compute"]);

            var runner = new global::PipelineRunner.PipelineRunner(
                new StepRegistry([
                    new ReplayDependencyStep(1, "Generate annotations parameters and raw tools"),
                    new ReplayDependencyStep(2, "Generate example prompts"),
                    new ReplayRecordingStep(),
                ]),
                new PipelineContextFactory(
                    new RecordingProcessRunner(),
                    new WorkspaceManager(),
                    new CliMetadataLoader(),
                    new TargetMatcher(),
                    new StubFilteredCliWriter(),
                    new StubBuildCoordinator(),
                    new StubAiCapabilityProbe(),
                    new BufferedReportWriter(),
                    repoRoot));

            var request = new PipelineRequest(
                "compute",
                [],
                ".\\generated",
                SkipBuild: true,
                SkipValidation: false,
                DryRun: false,
                Replay: true,
                ReplayFromRunId: "run-123",
                ReplayStepName: "compose-and-improve-tool-files");

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.FatalExitCode, exitCode);
            Assert.Contains("upstream artifact not found:", errorWriter.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    private static void CreateReplayMetadata(string outputPath, IReadOnlyList<string> namespaces)
    {
        var cliDirectory = Path.Combine(outputPath, "cli");
        Directory.CreateDirectory(cliDirectory);
        File.WriteAllText(Path.Combine(cliDirectory, "cli-output.json"), """{"results":[{"command":"compute list","name":"compute list","description":"desc"}]}""");
        File.WriteAllText(Path.Combine(cliDirectory, "cli-version.json"), """{"version":"1.2.3"}""");
        var namespaceJson = JsonSerializer.Serialize(new
        {
            results = namespaces.Select(name => new { name }),
        });
        File.WriteAllText(
            Path.Combine(cliDirectory, "cli-namespace.json"),
            namespaceJson);
    }

    private static void WriteStepEnvelope(string outputPath, int stepId, string stepSlug)
    {
        StepResultWriter.Write(Path.Combine(outputPath, $"step-{stepId}-{stepSlug}"), new StepResultFile
        {
            SchemaVersion = "1.0",
            Status = StepResultStatus.Success,
            Step = $"Step {stepId}",
            StepName = $"step-{stepId}-{stepSlug}",
        });
    }

    private sealed class RecordingStep : StepDefinition
    {
        private readonly List<string> _executionOrder;

        public RecordingStep(int id, string name, StepScope scope, List<string> executionOrder)
            : base(id, name, scope, FailurePolicy.Fatal, requiresCliOutput: false, requiresCliVersion: false)
        {
            _executionOrder = executionOrder;
        }

        public int Executions { get; private set; }

        public override ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            Executions++;
            _executionOrder.Add(Scope == StepScope.Global
                ? "global"
                : $"namespace:{context.Items["Namespace"]}");
            return ValueTask.FromResult(StepResult.DryRun(Array.Empty<string>()));
        }
    }

    private sealed class ReplayRecordingStep()
        : StepDefinition(3, "Compose and improve tool files", StepScope.Namespace, FailurePolicy.Fatal, dependsOn: [1, 2], requiresCliOutput: false, requiresCliVersion: false)
    {
        public int Executions { get; private set; }

        public string? LastOutputPath { get; private set; }

        public override ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            Executions++;
            LastOutputPath = context.OutputPath;
            return ValueTask.FromResult(StepResult.DryRun(Array.Empty<string>()));
        }
    }

    private sealed class ReplayDependencyStep(int id, string name)
        : StepDefinition(id, name, StepScope.Namespace, FailurePolicy.Fatal, requiresCliOutput: false, requiresCliVersion: false)
    {
        public override ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(StepResult.DryRun(Array.Empty<string>()));
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
            => ValueTask.FromResult<IReadOnlyList<string>>(["compute", "storage"]);

        private static CliMetadataSnapshot CreateSnapshot()
        {
            var json = JsonSerializer.Serialize(new
            {
                results = new[]
                {
                    new
                    {
                        command = "compute list",
                        name = "compute list",
                        description = "desc",
                    },
                    new
                    {
                        command = "storage list",
                        name = "storage list",
                        description = "desc",
                    },
                },
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
    }
}
