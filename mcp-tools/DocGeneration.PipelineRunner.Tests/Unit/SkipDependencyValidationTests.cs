using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class SkipDependencyValidationTests
{
    /// <summary>
    /// Step 4 depends on Step 3. Selecting only Step 4 without --skip-deps
    /// must return InvalidArgumentsExitCode (64) with an error mentioning the missing dependency.
    /// </summary>
    [Fact]
    public async Task RunAsync_WithoutSkipDeps_Step4Alone_ReturnsDepError()
    {
        var (runner, reportWriter) = CreateRunner();

        // Step 4 alone, SkipDependencyValidation = false (default)
        var request = new PipelineRequest(
            "compute",
            [4],
            ".\\generated-compute",
            SkipBuild: true,
            SkipValidation: false,
            DryRun: false,
            SkipEnvValidation: true);

        var exitCode = await runner.RunAsync(request, CancellationToken.None);

        Assert.Equal(global::PipelineRunner.PipelineRunner.InvalidArgumentsExitCode, exitCode);
        Assert.Contains(reportWriter.Messages, m => m.Contains("requires step(s)", StringComparison.Ordinal));
    }

    /// <summary>
    /// Step 4 depends on Step 3, but with --skip-deps the dependency check is bypassed.
    /// The runner should NOT return InvalidArgumentsExitCode for missing dependencies.
    /// (It may fail for other reasons such as missing CLI output — that's expected.)
    /// </summary>
    [Fact]
    public async Task RunAsync_WithSkipDeps_Step4Alone_SkipsDependencyCheck()
    {
        var (runner, reportWriter) = CreateRunner();

        // Step 4 alone, SkipDependencyValidation = true
        var request = new PipelineRequest(
            "compute",
            [4],
            ".\\generated-compute",
            SkipBuild: true,
            SkipValidation: false,
            DryRun: false,
            SkipEnvValidation: true,
            SkipDependencyValidation: true);

        var exitCode = await runner.RunAsync(request, CancellationToken.None);

        // Must NOT fail due to dependency validation
        Assert.DoesNotContain(reportWriter.Messages, m => m.Contains("requires step(s)", StringComparison.Ordinal));
    }

    /// <summary>
    /// Verify the CLI parser recognises --skip-deps and maps it to PipelineRequest.SkipDependencyValidation.
    /// </summary>
    [Fact]
    public void PipelineCli_ParsesSkipDepsFlag()
    {
        var result = PipelineCli.Parse(["--namespace", "compute", "--steps", "4", "--skip-deps"]);

        Assert.NotNull(result.Request);
        Assert.True(result.Request!.SkipDependencyValidation, "Expected SkipDependencyValidation to be true when --skip-deps is passed.");
    }

    /// <summary>
    /// Without --skip-deps, SkipDependencyValidation defaults to false.
    /// </summary>
    [Fact]
    public void PipelineCli_DefaultsSkipDepsToFalse()
    {
        var result = PipelineCli.Parse(["--namespace", "compute", "--steps", "4"]);

        Assert.NotNull(result.Request);
        Assert.False(result.Request!.SkipDependencyValidation, "Expected SkipDependencyValidation to default to false.");
    }

    // ---- helpers ----

    private static (global::PipelineRunner.PipelineRunner Runner, BufferedReportWriter ReportWriter) CreateRunner()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-skip-deps-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        var reportWriter = new BufferedReportWriter();
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

        var registry = StepRegistry.CreateDefault(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        var runner = new global::PipelineRunner.PipelineRunner(registry, contextFactory);
        return (runner, reportWriter);
    }

    /// <summary>
    /// Minimal ICliMetadataLoader that returns canned data so the runner can proceed past bootstrap.
    /// </summary>
    private sealed class StaticCliMetadataLoader : ICliMetadataLoader
    {
        private readonly CliMetadataSnapshot _snapshot = CreateSnapshot();

        public bool CliOutputExists(string outputPath) => true;
        public bool CliVersionExists(string outputPath) => true;
        public bool NamespaceMetadataExists(string outputPath) => true;

        public ValueTask<CliMetadataSnapshot> LoadCliOutputAsync(string outputPath, CancellationToken cancellationToken)
            => ValueTask.FromResult(_snapshot);

        public ValueTask<string> LoadCliVersionAsync(string outputPath, CancellationToken cancellationToken)
            => ValueTask.FromResult("1.0.0");

        public ValueTask<IReadOnlyList<string>> LoadNamespacesAsync(string outputPath, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<string>>(["compute", "storage"]);

        private static CliMetadataSnapshot CreateSnapshot()
        {
            var json = JsonSerializer.Serialize(new
            {
                results = new[]
                {
                    new { command = "compute list", name = "compute list", description = "desc" },
                    new { command = "storage list", name = "storage list", description = "desc" },
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

            return new CliMetadataSnapshot(
                Path.Combine(Path.GetTempPath(), $"cli-output-{Guid.NewGuid():N}.json"),
                root,
                tools);
        }
    }
}
