using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
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
            SeedFile(Path.Combine(outputPath, "tools", "compute-list.md"), "---\n---\n# Tool\n\n<!-- @mcpcli compute list -->\nBody\n");
            SeedFile(Path.Combine(outputPath, "tool-family", "compute.md"),
                "---\ntitle: Compute\ntool_count: 1\n---\n# Compute\n\n## List virtual machines\n<!-- @mcpcli compute list -->\nExample prompts include:\n- List resources where resource group name is 'rg-one'\n| Parameter | Required |\n| --- | --- |\n| resource group name | Yes |\n| location | No |\n\n## See also\n- Link\n");

            var context = new PipelineContext
            {
                Request = new PipelineRequest("compute", [4], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
                RepoRoot = testRoot,
                McpToolsRoot = Path.Combine(testRoot, "mcp-tools"),
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

    /// <summary>
    /// E2E test: runs Step 4 post-assembly validation on the advisor namespace.
    /// Confirms the extended validator fires, produces no false positives on a correctly-formed
    /// tool-family article, and the step envelope (validation.json) contains validationStatus
    /// "passed" with all 6 new check results.
    ///
    /// PRD-QUALITY-2026-05-30 Item C acceptance criteria: E2E coverage.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_AdvisorNamespace_ValidAssembly_StepEnvelopeContainsPassedStatusAndAllSixCheckResults()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-e2e-advisor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);

        try
        {
            var outputPath = Path.Combine(testRoot, "generated-advisor");
            Directory.CreateDirectory(outputPath);

            // Seed two advisor tool files — correctly formed, no tone markers, ≥2 params, has examples
            SeedFile(Path.Combine(outputPath, "tools", "advisor-list.md"),
                "---\n---\n# Advisor list\n\n<!-- @mcpcli advisor list -->\nBody\n");
            SeedFile(Path.Combine(outputPath, "tools", "advisor-show.md"),
                "---\n---\n# Advisor show\n\n<!-- @mcpcli advisor show -->\nBody\n");

            // Seed the tool-family article — satisfies all 6 new post-assembly validation checks:
            // 1. Related tools completeness: no backtick terms in See also that don't match headings
            // 2. Tone markers: no second-person, marketing superlatives, or deprecated service names
            // 3. Boilerplate redundancy: no context.json present so check is skipped
            // 4. Related section header: '## See also' is present
            // 5. Tool examples: every section has 'Example prompts include:'
            // 6. Parameter count: every section has ≥2 parameters
            SeedFile(Path.Combine(outputPath, "tool-family", "advisor.md"), """
                ---
                title: Advisor tools
                tool_count: 2
                ---
                # Advisor tools

                ## List recommendations
                <!-- @mcpcli advisor list -->
                Example prompts include:
                - List all Advisor recommendations for subscription 'sub-prod'
                - List high-severity recommendations in category 'Cost' for subscription 'sub-dev'
                | Parameter | Required |
                | --- | --- |
                | subscription | No |
                | category | No |

                ## Show recommendation details
                <!-- @mcpcli advisor show -->
                Example prompts include:
                - Show the Advisor recommendation with ID 'rec-001' in resource group 'rg-main'
                | Parameter | Required |
                | --- | --- |
                | recommendation id | Yes |
                | resource group | No |

                ## See also
                - Link
                """);

            var context = new PipelineContext
            {
                Request = new PipelineRequest("advisor", [4], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
                RepoRoot = testRoot,
                McpToolsRoot = Path.Combine(testRoot, "mcp-tools"),
                OutputPath = outputPath,
                ProcessRunner = new RecordingProcessRunner(),
                Workspaces = new WorkspaceManager(),
                CliMetadataLoader = new StubCliMetadataLoader(),
                TargetMatcher = new TargetMatcher(),
                FilteredCliWriter = new StubFilteredCliWriter(),
                BuildCoordinator = new StubBuildCoordinator(),
                AiCapabilityProbe = new StubAiCapabilityProbe(),
                Reports = new BufferedReportWriter(),
                CliVersion = "1.2.3",
                SelectedNamespaces = ["advisor"],
            };
            context.Items[ToolFamilyPostAssemblyValidator.FamilyNameContextKey] = "advisor";

            var validator = new ToolFamilyPostAssemblyValidator();
            var validatorResult = await validator.ValidateAsync(context, new IntegrationStep(), CancellationToken.None);

            // 1. Validator fires and reports success (no false positives)
            Assert.True(validatorResult.Success,
                $"Expected no blocking issues for valid advisor assembly.\nWarnings:\n{string.Join("\n", validatorResult.Warnings)}");
            Assert.Equal("ToolFamilyPostAssemblyValidator", validatorResult.Name);

            // 2. Report file contains all 6 new validation check sections
            var reportPath = Path.Combine(outputPath, "reports", "tool-family-validation-advisor.txt");
            var reportText = File.ReadAllText(reportPath);
            Assert.Contains("Related tools completeness: ✅ PASS", reportText, StringComparison.Ordinal);
            Assert.Contains("Tone markers: ✅ none detected", reportText, StringComparison.Ordinal);
            Assert.Contains("Boilerplate redundancy: ✅ none detected", reportText, StringComparison.Ordinal);
            Assert.Contains("Related section header: ✅ present", reportText, StringComparison.Ordinal);
            Assert.Contains("Tool examples:", reportText, StringComparison.Ordinal);
            Assert.Contains("Parameter count:", reportText, StringComparison.Ordinal);
            Assert.Contains("RESULT: PASS", reportText, StringComparison.Ordinal);

            // 3. Step envelope (validation.json written by ObservabilityWriter) has validationStatus = "passed"
            var envelopeDir = Path.Combine(testRoot, "envelope-advisor");
            Directory.CreateDirectory(envelopeDir);
            ObservabilityWriter.WriteValidation(envelopeDir, "Generate tool-family article", [validatorResult]);

            var validationJson = File.ReadAllText(Path.Combine(envelopeDir, StageOutputContract.ValidationFileName));
            using var doc = JsonDocument.Parse(validationJson);
            var root = doc.RootElement;

            Assert.Equal("passed", root.GetProperty("overallStatus").GetString());
            var validatorResults = root.GetProperty("validatorResults");
            Assert.Equal(1, validatorResults.GetArrayLength());
            var firstResult = validatorResults[0];
            Assert.Equal("ToolFamilyPostAssemblyValidator", firstResult.GetProperty("Name").GetString());
            Assert.True(firstResult.GetProperty("Success").GetBoolean());
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
        public int MaxRetries => 0;
        public ValueTask<global::PipelineRunner.Contracts.StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(global::PipelineRunner.Contracts.StepResult.DryRun(Array.Empty<string>()));
    }
}
