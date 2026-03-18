using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Validation;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class ToolFamilyPostAssemblyValidatorTests
{
    [Fact]
    public async Task ValidateAsync_PassScenario_ReturnsSuccessfulResult()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-show.md"), "compute show");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), ValidArticleContent());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Empty(result.Warnings);
            Assert.Equal("ToolFamilyPostAssemblyValidator", result.Name);
            Assert.Contains("RESULT: PASS", File.ReadAllText(Path.Combine(context.OutputPath, "reports", "tool-family-validation-compute.txt")), StringComparison.Ordinal);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_BlockingFailure_ReturnsFailedResult()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-show.md"), "compute show");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), InvalidArticleContentWithWrongCount());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, warning => warning.Contains("Blocking: Tool count integrity check failed.", StringComparison.Ordinal));
            Assert.Contains("RESULT: FAIL", File.ReadAllText(Path.Combine(context.OutputPath, "reports", "tool-family-validation-compute.txt")), StringComparison.Ordinal);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_WarningScenario_AccumulatesWarnings()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), WarningArticleContent());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains(result.Warnings, warning => warning.Contains("example prompt header is Examples:", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, warning => warning.Contains("missing 'resource group name'", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, warning => warning.Contains("Branding: Use \"this tool\" instead of \"this command\".", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_AngleBracketPlaceholders_AcceptedAsValidParameterMentions()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), AngleBracketPlaceholderContent());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("missing", StringComparison.OrdinalIgnoreCase)
                && warning.Contains("in example prompt", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_BacktickPlaceholders_AcceptedAsValidParameterMentions()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), BacktickPlaceholderContent());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("missing", StringComparison.OrdinalIgnoreCase)
                && warning.Contains("in example prompt", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_DescriptivePlaceholders_MatchesParametersByWordOverlap()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), DescriptivePlaceholderContent());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.True(result.Success);
            // <key_name> matches parameter "key" (word "key" is a token in "key_name")
            // <resource_group_name> matches parameter "resource group" (words "resource" and "group" are tokens)
            Assert.DoesNotContain(result.Warnings, warning =>
                warning.Contains("missing", StringComparison.OrdinalIgnoreCase)
                && warning.Contains("in example prompt", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static string DescriptivePlaceholderContent()
        => """
        ---
        title: Compute tools
        tool_count: 1
        ---
        # Compute tools

        ## Set configuration value
        <!-- @mcpcli compute list -->
        Example prompts include:
        - Set the key <key_name> in resource group <resource_group_name>
        | Parameter | Required |
        | --- | --- |
        | key | Yes |
        | resource group | Yes |

        ## Related content
        - Link
        """;

    private static string AngleBracketPlaceholderContent()
        => """
        ---
        title: Compute tools
        tool_count: 1
        ---
        # Compute tools

        ## Set configuration value
        <!-- @mcpcli compute list -->
        Example prompts include:
        - Set the key <key> in App Configuration store <account> to <value>
        | Parameter | Required |
        | --- | --- |
        | account | Yes |
        | key | Yes |
        | value | Yes |

        ## Related content
        - Link
        """;

    private static string BacktickPlaceholderContent()
        => """
        ---
        title: Compute tools
        tool_count: 1
        ---
        # Compute tools

        ## Set configuration value
        <!-- @mcpcli compute list -->
        Example prompts include:
        - Delete the key `key` in App Configuration store `account`
        - Get the secret `<key>` from vault `<account>`
        | Parameter | Required |
        | --- | --- |
        | account | Yes |
        | key | Yes |

        ## Related content
        - Link
        """;

    private static PipelineContext CreateContext(string testRoot)
    {
        var outputPath = Path.Combine(testRoot, "generated-compute");
        Directory.CreateDirectory(outputPath);

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
            CliVersion = "1.2.3",
            SelectedNamespaces = ["compute"],
        };

        context.Items[ToolFamilyPostAssemblyValidator.FamilyNameContextKey] = "compute";
        return context;
    }

    private static string ValidArticleContent()
        => """
        ---
        title: Compute tools
        tool_count: 2
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        Example prompts include:
        - List resources where resource group name is 'rg-one'
        | Parameter | Required |
        | --- | --- |
        | resource group name | Yes |

        ## Show virtual machine
        <!-- @mcpcli compute show -->
        Example prompts include:
        - Show the VM named 'vm-one'
        | Parameter | Required |
        | --- | --- |
        | vm name | Yes |

        ## Related content
        - Link
        """;

    private static string InvalidArticleContentWithWrongCount()
        => """
        ---
        title: Compute tools
        tool_count: 1
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        Example prompts include:
        - List resources where resource group name is 'rg-one'
        | Parameter | Required |
        | --- | --- |
        | resource group name | Yes |

        ## Related content
        - Link
        """;

    private static string WarningArticleContent()
        => """
        ---
        title: Compute tools
        tool_count: 1
        ---
        # Compute tools

        This command helps with compute resources.

        ## List virtual machines
        <!-- @mcpcli compute list -->
        Examples:
        - List resources with <resource-group-name>
        | Parameter | Required |
        | --- | --- |
        | resource group name | Yes |

        ## Related content
        - Link
        """;

    private static void SeedToolFile(string path, string command)
        => SeedFile(path, $"---\n---\n# Tool\n\n<!-- @mcpcli {command} -->\nBody\n");

    private static void SeedFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string CreateTestRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pipeline-runner-validator-tests-{Guid.NewGuid():N}");
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

    private sealed class FakeStep : IPipelineStep
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
