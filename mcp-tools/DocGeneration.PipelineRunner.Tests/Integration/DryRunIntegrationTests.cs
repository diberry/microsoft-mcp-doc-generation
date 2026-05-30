using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Shared;
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
            var stepRegistry = StepRegistry.CreateDefault(Path.Combine(repoRoot, "mcp-tools", "scripts"));
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
                stepRegistry,
                contextFactory);

            var request = new PipelineRequest("compute", new[] { 1, 2, 3, 4, 5, 6 }, ".\\generated-compute", false, false, true, SkipChangelogGate: true);
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

            var outputRoot = Path.GetFullPath(Path.Combine(repoRoot, "generated-compute"));
            var selectedSteps = stepRegistry.GetOrderedSteps(request.Steps).ToDictionary(step => step.Id);
            var step1Directory = Path.Combine(outputRoot, global::PipelineRunner.PipelineRunner.GetStepIdentifierSlug(selectedSteps[1]));
            var step2Directory = Path.Combine(outputRoot, global::PipelineRunner.PipelineRunner.GetStepIdentifierSlug(selectedSteps[2]));
            var step3Directory = Path.Combine(outputRoot, global::PipelineRunner.PipelineRunner.GetStepIdentifierSlug(selectedSteps[3]));

            Assert.True(StepResultReader.TryRead(step1Directory, out var step1Envelope));
            Assert.NotNull(step1Envelope);
            Assert.Equal(global::PipelineRunner.PipelineRunner.GetStepIdentifierSlug(selectedSteps[1]), step1Envelope!.StepName);

            Assert.True(StepResultReader.TryRead(step2Directory, out var step2Envelope));
            Assert.NotNull(step2Envelope);
            Assert.Equal(global::PipelineRunner.PipelineRunner.GetStepIdentifierSlug(selectedSteps[2]), step2Envelope!.StepName);

            Assert.True(StepResultReader.TryRead(step3Directory, out var step3Envelope));
            Assert.NotNull(step3Envelope);
            Assert.Equal(global::PipelineRunner.PipelineRunner.GetStepIdentifierSlug(selectedSteps[3]), step3Envelope!.StepName);
            Assert.Equal(StepResultStatus.Success, step3Envelope.Status);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }
}
