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

            var runner = new global::PipelineRunner.PipelineRunner(
                new StepRegistry([new FakeStep(new FixedValidator(success: false, warnings: ["validator failed"]))]),
                contextFactory);

            var request = new PipelineRequest("compute", [4], ".\\generated-compute", SkipBuild: true, SkipValidation: false, DryRun: false);
            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.FatalExitCode, exitCode);
            Assert.Contains(reportWriter.Messages, message => message.Contains("Validator: FixedValidator", StringComparison.Ordinal));
            Assert.Contains(reportWriter.Messages, message => message.Contains("validator failed", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    private sealed class FakeStep : StepDefinition
    {
        public FakeStep(IPostValidator validator)
            : base(4, "Generate tool-family article", StepScope.Namespace, FailurePolicy.Fatal, postValidators: [validator])
        {
        }

        public override ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(new StepResult(true, Array.Empty<string>(), TimeSpan.Zero, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ValidatorResult>()));
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
