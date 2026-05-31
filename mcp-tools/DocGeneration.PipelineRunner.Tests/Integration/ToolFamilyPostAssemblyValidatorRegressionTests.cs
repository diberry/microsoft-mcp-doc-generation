using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Validation;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Integration;

/// <summary>
/// Regression tests verifying the 6 new post-assembly validation checks produce no false positives
/// on correctly-formed tool-family articles for three representative namespaces:
/// advisor, compute, and monitor.
///
/// PRD-QUALITY-2026-05-30 Item C acceptance criteria: regression coverage.
/// </summary>
public class ToolFamilyPostAssemblyValidatorRegressionTests
{
    [Fact]
    public async Task Validate_Advisor_ProducesNoFalsePositives_ForAllNewChecks()
    {
        await RunRegressionAsync(
            "advisor",
            AdvisorArticleContent(),
            [
                ("advisor-list.md", "advisor list"),
                ("advisor-show.md", "advisor show"),
            ]);
    }

    [Fact]
    public async Task Validate_Compute_ProducesNoFalsePositives_ForAllNewChecks()
    {
        await RunRegressionAsync(
            "compute",
            ComputeArticleContent(),
            [
                ("compute-list.md", "compute list"),
                ("compute-create.md", "compute create"),
            ]);
    }

    [Fact]
    public async Task Validate_Monitor_ProducesNoFalsePositives_ForAllNewChecks()
    {
        await RunRegressionAsync(
            "monitor",
            MonitorArticleContent(),
            [
                ("monitor-list.md", "monitor list"),
                ("monitor-query.md", "monitor query"),
            ]);
    }

    private static async Task RunRegressionAsync(
        string namespaceName,
        string articleContent,
        (string fileName, string command)[] toolFiles)
    {
        var testRoot = CreateTestRoot(namespaceName);
        try
        {
            var context = CreateContext(testRoot, namespaceName);
            foreach (var (fileName, command) in toolFiles)
            {
                SeedToolFile(Path.Combine(context.OutputPath, "tools", fileName), command);
            }

            SeedFile(Path.Combine(context.OutputPath, "tool-family", $"{namespaceName}.md"), articleContent);

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new RegressionStep(), CancellationToken.None);

            var reportPath = Path.Combine(context.OutputPath, "reports", $"tool-family-validation-{namespaceName}.txt");
            var reportText = File.ReadAllText(reportPath);

            // No blocking issues — no false positives on any of the 6 new checks
            Assert.True(result.Success,
                $"Namespace '{namespaceName}' produced false positives:\n{string.Join("\n", result.Warnings)}\n\nReport:\n{reportText}");

            // All 6 new check result sections must appear in the report
            Assert.Contains("Related tools completeness:", reportText, StringComparison.Ordinal);
            Assert.Contains("Tone markers:", reportText, StringComparison.Ordinal);
            Assert.Contains("Boilerplate redundancy:", reportText, StringComparison.Ordinal);
            Assert.Contains("Related section header:", reportText, StringComparison.Ordinal);
            Assert.Contains("Tool examples:", reportText, StringComparison.Ordinal);
            Assert.Contains("Parameter count:", reportText, StringComparison.Ordinal);

            // All 6 checks must show their passing state
            Assert.Contains("Related tools completeness: ✅ PASS", reportText, StringComparison.Ordinal);
            Assert.Contains("Tone markers: ✅ none detected", reportText, StringComparison.Ordinal);
            Assert.Contains("Boilerplate redundancy: ✅ none detected", reportText, StringComparison.Ordinal);
            Assert.Contains("Related section header: ✅ present", reportText, StringComparison.Ordinal);
            Assert.Contains("RESULT: PASS", reportText, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    // Synthetic valid article for the advisor namespace.
    // Satisfies all 6 checks: no tone markers, has related-tools section, has examples,
    // ≥2 params per tool, sections differ enough to avoid boilerplate redundancy,
    // related-tools back-references only sections present in the article.
    private static string AdvisorArticleContent()
        => """
        ---
        title: Advisor tools
        tool_count: 2
        ---
        # Advisor tools

        ## List recommendations
        <!-- @mcpcli advisor list -->
        Example prompts include:
        - List all Advisor recommendations for subscription 'sub-prod'
        - Show high-severity Advisor recommendations in category 'Cost'
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
        """;

    // Synthetic valid article for the compute namespace.
    private static string ComputeArticleContent()
        => """
        ---
        title: Compute tools
        tool_count: 2
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        Example prompts include:
        - List all virtual machines in resource group 'rg-prod'
        - Show virtual machines in location 'eastus'
        | Parameter | Required |
        | --- | --- |
        | resource group name | Yes |
        | location | No |

        ## Create virtual machine
        <!-- @mcpcli compute create -->
        Example prompts include:
        - Create a virtual machine with vm name 'vm-test' in resource group 'rg-dev'
        | Parameter | Required |
        | --- | --- |
        | vm name | Yes |
        | resource group name | Yes |

        ## See also
        - Link
        """;

    // Synthetic valid article for the monitor namespace.
    private static string MonitorArticleContent()
        => """
        ---
        title: Monitor tools
        tool_count: 2
        ---
        # Monitor tools

        ## List activity logs
        <!-- @mcpcli monitor list -->
        Example prompts include:
        - List activity logs for resource group 'rg-prod' from the last 24 hours
        | Parameter | Required |
        | --- | --- |
        | resource group | Yes |
        | start time | No |

        ## Query metrics
        <!-- @mcpcli monitor query -->
        Example prompts include:
        - Query metric names 'Percentage CPU' for resource 'vm-prod' in namespace 'Microsoft.Compute/virtualMachines'
        - Show resource 'vm-prod' metric names 'Disk Read Ops' for the last hour
        | Parameter | Required |
        | --- | --- |
        | resource id | Yes |
        | metric names | Yes |

        ## See also
        - Link
        """;

    private static PipelineContext CreateContext(string testRoot, string namespaceName)
    {
        var outputPath = Path.Combine(testRoot, $"generated-{namespaceName}");
        Directory.CreateDirectory(outputPath);

        var context = new PipelineContext
        {
            Request = new PipelineRequest(namespaceName, [4], outputPath, SkipBuild: true, SkipValidation: false, DryRun: false),
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
            SelectedNamespaces = [namespaceName],
        };

        context.Items[ToolFamilyPostAssemblyValidator.FamilyNameContextKey] = namespaceName;
        return context;
    }

    private static void SeedToolFile(string path, string command)
        => SeedFile(path, $"---\n---\n# Tool\n\n<!-- @mcpcli {command} -->\nBody\n");

    private static void SeedFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string CreateTestRoot(string namespaceName)
    {
        var root = Path.Combine(Path.GetTempPath(), $"pipeline-runner-regression-{namespaceName}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTestRoot(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class RegressionStep : IPipelineStep
    {
        public int Id => 4;
        public string Name => "Generate tool-family article";
        public StepScope Scope => StepScope.Namespace;
        public FailurePolicy FailurePolicy => FailurePolicy.Fatal;
        public IReadOnlyList<int> DependsOn => Array.Empty<int>();
        public IReadOnlyList<IPostValidator> PostValidators => Array.Empty<IPostValidator>();
        public int MaxRetries => 0;

        public ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(StepResult.DryRun(Array.Empty<string>()));
    }
}
