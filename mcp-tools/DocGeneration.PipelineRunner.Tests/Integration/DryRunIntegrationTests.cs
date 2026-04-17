using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Integration;

public class DryRunIntegrationTests
{
    [Fact]
    public async Task DryRun_PrintsPlanWithoutExecutingProcesses()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-dry-run-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var processRunner = new RecordingProcessRunner();
            var reportWriter = new BufferedReportWriter();
            var contextFactory = new PipelineContextFactory(
                processRunner,
                new WorkspaceManager(),
                new StubCliMetadataLoader(),
                new TargetMatcher(),
                new StubFilteredCliWriter(),
                new StubBuildCoordinator(),
                new StubAiCapabilityProbe(),
                reportWriter,
                repoRoot);

            var runner = new global::PipelineRunner.PipelineRunner(
                StepRegistry.CreateDefault(Path.Combine(repoRoot, "mcp-tools", "scripts")),
                contextFactory);

            var request = new PipelineRequest("compute", new[] { 1, 2, 3, 4, 5, 6 }, ".\\generated-compute", false, false, true);
            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Empty(processRunner.Invocations);
            Assert.Contains(reportWriter.Messages, message => message.Contains("Dry run plan", StringComparison.Ordinal));
            Assert.Contains(reportWriter.Messages, message => message.Contains("Step 0: Bootstrap pipeline [Global, Fatal, Typed]", StringComparison.Ordinal));
            Assert.Contains(reportWriter.Messages, message => message.Contains("Step 1: Generate annotations, parameters, and raw tools [Namespace, Fatal, Typed]", StringComparison.Ordinal));
            Assert.Contains(reportWriter.Messages, message => message.Contains("Step 4: Generate tool-family article [Namespace, Fatal, Typed]", StringComparison.Ordinal));
            Assert.Contains(reportWriter.Messages, message => message.Contains("Step 5: Generate skills relevance [Namespace, Warn, Typed]", StringComparison.Ordinal));
            Assert.Contains(reportWriter.Messages, message => message.Contains("Step 6: Generate horizontal article [Namespace, Fatal, Typed]", StringComparison.Ordinal));
            Assert.Contains(reportWriter.Messages, message => message.Contains("Post-validators: ToolFamilyPostAssemblyValidator", StringComparison.Ordinal));
            Assert.Contains(reportWriter.Messages, message => message.Contains("Dependency check: passed", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }
}
