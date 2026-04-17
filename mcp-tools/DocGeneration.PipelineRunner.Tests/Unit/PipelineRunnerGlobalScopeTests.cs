using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
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

            var request = new PipelineRequest(null, [1], ".\\generated", SkipBuild: true, SkipValidation: false, DryRun: false);
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
