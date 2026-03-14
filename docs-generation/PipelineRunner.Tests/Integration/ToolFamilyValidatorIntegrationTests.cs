using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Services;
using PipelineRunner.Validation;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Integration;

public class ToolFamilyValidatorIntegrationTests
{
    [Fact]
    public async Task ValidateAsync_WritesStructuredReportAndResult()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-validator-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);

        try
        {
            var outputPath = Path.Combine(testRoot, "generated-compute");
            Directory.CreateDirectory(outputPath);
            SeedFile(Path.Combine(outputPath, "tools", "compute-list.md"), "---\n---\n# Tool\n\n<!-- @mcpcli compute list -->\nBody\n<!-- @mcpcli compute list -->\n");
            SeedFile(Path.Combine(outputPath, "tool-family", "compute.md"),
                "---\ntitle: Compute\ntool_count: 1\n---\n# Compute\n\n## List virtual machines\n<!-- @mcpcli compute list -->\n<!-- @mcpcli compute list -->\nExample prompts include:\n- List resources where resource group name is 'rg-one'\n| Parameter | Required |\n| --- | --- |\n| resource group name | Yes |\n\n## Related content\n- Link\n");

            var context = new PipelineContext
            {
                Request = new PipelineRequest("compute", [4], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
                RepoRoot = testRoot,
                DocsGenerationRoot = Path.Combine(testRoot, "docs-generation"),
                OutputPath = outputPath,
                ProcessRunner = new RecordingProcessRunner(),
                Workspaces = new WorkspaceManager(),
                CliMetadataLoader = new StubCliMetadataLoader(),
                TargetMatcher = new TargetMatcher(),
                FilteredCliWriter = new StubFilteredCliWriter(),
                BuildCoordinator = new StubBuildCoordinator(),
                AiCapabilityProbe = new StubAiCapabilityProbe(),
                Reports = new BufferedReportWriter(),
            };
            context.Items[ToolFamilyPostAssemblyValidator.FamilyNameContextKey] = "compute";

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new IntegrationStep(), CancellationToken.None);
            var reportPath = Path.Combine(outputPath, "reports", "tool-family-validation-compute.txt");
            var reportText = File.ReadAllText(reportPath);

            Assert.True(result.Success);
            Assert.Equal("ToolFamilyPostAssemblyValidator", result.Name);
            Assert.Empty(result.Warnings);
            Assert.Contains("Tool files found: 1", reportText, StringComparison.Ordinal);
            Assert.Contains("RESULT: PASS", reportText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static void SeedFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private sealed class IntegrationStep : global::PipelineRunner.Contracts.IPipelineStep
    {
        public int Id => 4;
        public string Name => "Generate tool-family article";
        public global::PipelineRunner.Contracts.StepScope Scope => global::PipelineRunner.Contracts.StepScope.Namespace;
        public global::PipelineRunner.Contracts.FailurePolicy FailurePolicy => global::PipelineRunner.Contracts.FailurePolicy.Fatal;
        public IReadOnlyList<int> DependsOn => Array.Empty<int>();
        public IReadOnlyList<global::PipelineRunner.Contracts.IPostValidator> PostValidators => Array.Empty<global::PipelineRunner.Contracts.IPostValidator>();
        public ValueTask<global::PipelineRunner.Contracts.StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(global::PipelineRunner.Contracts.StepResult.DryRun(Array.Empty<string>()));
    }
}
