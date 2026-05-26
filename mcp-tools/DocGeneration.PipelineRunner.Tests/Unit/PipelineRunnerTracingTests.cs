using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class PipelineRunnerTracingTests
{
    [Fact]
    public async Task RunAsync_WritesGlobalAndNamespaceTraceArtifacts()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-tracing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var outputRoot = Path.Combine(repoRoot, "generated");
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

            var runner = new global::PipelineRunner.PipelineRunner(
                new StepRegistry([new GlobalSuccessStep(), new NamespaceSuccessStep()]),
                contextFactory);

            var request = new PipelineRequest(
                "compute",
                [0, 2],
                ".\\generated",
                SkipBuild: true,
                SkipValidation: false,
                DryRun: false,
                SkipChangelogGate: true);

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);

            var globalTracePath = Path.Combine(outputRoot, "trace", "pipeline-trace.json");
            var namespaceTracePath = Path.Combine(repoRoot, "generated-compute", "trace", "pipeline-trace.json");

            Assert.True(File.Exists(globalTracePath), "Global trace should be flushed to the root output trace directory.");
            Assert.True(File.Exists(namespaceTracePath), "Namespace trace should be flushed to the namespace-specific trace directory.");

            using var globalTrace = JsonDocument.Parse(await File.ReadAllTextAsync(globalTracePath));
            using var namespaceTrace = JsonDocument.Parse(await File.ReadAllTextAsync(namespaceTracePath));

            var globalStep = Assert.Single(globalTrace.RootElement.GetProperty("steps").EnumerateArray());
            Assert.Equal("Bootstrap pipeline", globalStep.GetProperty("stepName").GetString());
            Assert.Equal("deterministic", globalStep.GetProperty("stepType").GetString());

            var namespaceStep = Assert.Single(namespaceTrace.RootElement.GetProperty("steps").EnumerateArray());
            Assert.Equal("Generate example prompts", namespaceStep.GetProperty("stepName").GetString());
            Assert.Equal("ai", namespaceStep.GetProperty("stepType").GetString());
            Assert.Equal("compute", namespaceStep.GetProperty("targetName").GetString());
        }
        finally
        {
            if (Directory.Exists(repoRoot))
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }
    }

    private sealed class GlobalSuccessStep()
        : StepDefinition(0, "Bootstrap pipeline", StepScope.Global, FailurePolicy.Fatal, requiresCliOutput: false, requiresCliVersion: false)
    {
        public override ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(StepResult.DryRun(Array.Empty<string>()));
    }

    private sealed class NamespaceSuccessStep()
        : StepDefinition(2, "Generate example prompts", StepScope.Namespace, FailurePolicy.Fatal, requiresCliOutput: false, requiresCliVersion: false)
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
        public ValueTask<CliMetadataSnapshot> LoadCliOutputAsync(string outputPath, CancellationToken cancellationToken) => ValueTask.FromResult(_snapshot);
        public ValueTask<string> LoadCliVersionAsync(string outputPath, CancellationToken cancellationToken) => ValueTask.FromResult("1.2.3");
        public ValueTask<IReadOnlyList<string>> LoadNamespacesAsync(string outputPath, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<string>>(["compute"]);

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
