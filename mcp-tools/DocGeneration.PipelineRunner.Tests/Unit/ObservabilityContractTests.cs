using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class ObservabilityContractTests
{
    [Fact]
    public void StageOutputContract_GetExpectedFiles_UsesDeterministicPromptPlaceholder()
    {
        var contract = new StageOutputContract(
            "Generate annotations",
            Path.Combine("C:", "repo", "generated", "observability", "1-generate-annotations"),
            IsDeterministic: true);

        var expectedFiles = contract.GetExpectedFiles();

        Assert.Equal(5, expectedFiles.Count);
        Assert.Contains(expectedFiles, path => path.EndsWith(Path.Combine("1-generate-annotations", "prompt-preview-na.txt"), StringComparison.Ordinal));
        Assert.DoesNotContain(expectedFiles, path => path.EndsWith("prompt-preview.txt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_DeterministicStep_WritesObservabilityFilesWithoutWarnings()
    {
        var repoRoot = CreateRepoRoot("pipeline-runner-observability-deterministic");
        var outputRelativePath = Path.Combine(".", "generated-compute");

        try
        {
            var reportWriter = new BufferedReportWriter();
            var step = new ObservabilityRecordingStep(
                id: 1,
                name: "Generate annotations",
                success: true,
                outputsFactory: context =>
                {
                    var artifactPath = Path.Combine(context.OutputPath, "annotations", "compute-list.md");
                    Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
                    File.WriteAllText(artifactPath, "# compute list");
                    return [artifactPath];
                });

            var runner = CreateRunner(repoRoot, reportWriter, step);
            var request = new PipelineRequest("compute", [1], outputRelativePath, SkipBuild: true, SkipValidation: false, DryRun: false);

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);

            var observabilityDirectory = Path.Combine(
                Path.GetFullPath(Path.Combine(repoRoot, outputRelativePath)),
                "observability",
                "1-generate-annotations");

            Assert.True(File.Exists(Path.Combine(observabilityDirectory, StageOutputContract.SummaryFileName)));
            Assert.True(File.Exists(Path.Combine(observabilityDirectory, StageOutputContract.StepResultFileName)));
            Assert.True(File.Exists(Path.Combine(observabilityDirectory, StageOutputContract.ValidationFileName)));
            Assert.True(File.Exists(Path.Combine(observabilityDirectory, StageOutputContract.PromptPreviewNaFileName)));
            Assert.True(File.Exists(Path.Combine(observabilityDirectory, StageOutputContract.MetricsFileName)));
            Assert.DoesNotContain(reportWriter.Messages, message => message.Contains("observability contract", StringComparison.OrdinalIgnoreCase));

            using var metricsDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(observabilityDirectory, StageOutputContract.MetricsFileName)));
            Assert.Equal("skipped", metricsDocument.RootElement.GetProperty("validationStatus").GetString());
            Assert.Equal(1, metricsDocument.RootElement.GetProperty("outputArtifactCount").GetInt32());
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_AiStep_MissingPromptPreviewLogsWarning()
    {
        var repoRoot = CreateRepoRoot("pipeline-runner-observability-ai");
        var outputRelativePath = Path.Combine(".", "generated-compute");

        try
        {
            var reportWriter = new BufferedReportWriter();
            var step = new ObservabilityRecordingStep(
                id: 2,
                name: "Generate example prompts",
                success: true);

            var runner = CreateRunner(repoRoot, reportWriter, step);
            var request = new PipelineRequest("compute", [2], outputRelativePath, SkipBuild: true, SkipValidation: false, DryRun: false);

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            Assert.Contains(
                reportWriter.Messages,
                message => message.Contains("prompt-preview.txt", StringComparison.Ordinal)
                    && message.Contains("observability contract", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    private static global::PipelineRunner.PipelineRunner CreateRunner(string repoRoot, BufferedReportWriter reportWriter, IPipelineStep step)
    {
        var contextFactory = new PipelineContextFactory(
            new RecordingProcessRunner(),
            new WorkspaceManager(),
            new StaticCliMetadataLoader(),
            new TargetMatcher(),
            new StubFilteredCliWriter(),
            new StubBuildCoordinator(),
            new StubAiCapabilityProbe(),
            reportWriter,
            repoRoot);

        return new global::PipelineRunner.PipelineRunner(new StepRegistry([step]), contextFactory);
    }

    private static string CreateRepoRoot(string prefix)
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);
        return repoRoot;
    }

    private sealed class ObservabilityRecordingStep(
        int id,
        string name,
        bool success,
        Func<PipelineContext, IReadOnlyList<string>>? outputsFactory = null)
        : StepDefinition(id, name, StepScope.Namespace, FailurePolicy.Fatal)
    {
        public override ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            var outputs = outputsFactory?.Invoke(context) ?? Array.Empty<string>();
            return ValueTask.FromResult(new StepResult(success, Array.Empty<string>(), TimeSpan.FromSeconds(2), outputs, Array.Empty<string>(), Array.Empty<ValidatorResult>(), Array.Empty<ArtifactFailure>()));
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
