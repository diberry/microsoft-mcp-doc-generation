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
    public async Task ValidateAsync_ToneMarkerPhrase_ReturnsWarning()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), ArticleWithToneMarkerPhrase());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains(result.Warnings, warning =>
                warning.Contains("Tone marker", StringComparison.Ordinal)
                && warning.Contains("you can", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_ToneMarkerInBulletItem_IsDetected()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), ArticleWithToneMarkerInBulletItem());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains(result.Warnings, warning =>
                warning.Contains("Tone marker", StringComparison.Ordinal)
                && warning.Contains("you can", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_ToneMarkerInTableRow_NotDetected()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), ArticleWithToneMarkerOnlyInTableRow());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("Tone marker", StringComparison.Ordinal));
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("documented parameter", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_ToneMarkerInsideFencedCodeBlock_NotDetected()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), ArticleWithToneMarkerOnlyInFencedCodeBlock());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("Tone marker", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_ExternalToolReferenceInRelatedSection_NotFlagged()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), ArticleWithExternalRelatedToolReferences());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("az vm list", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("referenced in related section", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_InternalToolReferencedInRelatedSection_WithNoH2_IsBlocking()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute compute-list");
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-show.md"), "compute compute-show");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), ArticleWithInternalRelatedToolMissingH2());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, warning =>
                warning.Contains("'compute-show' is referenced in the related section", StringComparison.OrdinalIgnoreCase)
                && warning.Contains("no matching H2 section", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_SpaceFormInternalToolReferencedInRelatedSection_WithNoH2_IsBlocking()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-show.md"), "compute show");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), ArticleWithSpaceFormInternalRelatedToolMissingH2());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, warning =>
                warning.Contains("Blocking: 🛑 'compute show' is referenced in the related section", StringComparison.OrdinalIgnoreCase)
                && warning.Contains("no matching H2 section", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_InternalToolReferencedInRelatedSection_WithMatchingH2_NotFlagged()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute compute-list");
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-show.md"), "compute compute-show");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), ArticleWithInternalRelatedToolMatchingH2());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.DoesNotContain(result.Warnings, warning =>
                warning.Contains("compute-show", StringComparison.OrdinalIgnoreCase)
                && warning.Contains("no matching H2 section", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_MissingRelatedSection_ReturnsWarning()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), ArticleWithoutRelatedSection());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains(result.Warnings, warning =>
                warning.Contains("Related section", StringComparison.Ordinal)
                && warning.Contains("absent", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_AlternateExampleHeader_NotBlocking()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), ArticleWithAlternateExampleHeader());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.DoesNotContain(result.Warnings, warning => warning.Contains("no example prompt header", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_ToolWithNoExampleHeader_IsBlocking()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), ArticleWithMissingExampleHeader());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, warning =>
                warning.Contains("list", StringComparison.OrdinalIgnoreCase)
                && warning.Contains("no example prompt header", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_ToolWithOneParameter_EmitsLowParamWarning()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), ArticleWithOneParameter());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains(result.Warnings, warning =>
                (warning.Contains("compute-list", StringComparison.OrdinalIgnoreCase)
                    || warning.Contains("list", StringComparison.OrdinalIgnoreCase))
                && (warning.Contains("1 documented parameter", StringComparison.OrdinalIgnoreCase)
                    || warning.Contains("only 1", StringComparison.OrdinalIgnoreCase)));
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
            Assert.Contains(result.Warnings, warning => warning.Contains("Blocking: Tool count integrity check failed (", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, warning => warning.Contains("frontmatter tool_count: 1, actual tool files: 2", StringComparison.Ordinal));
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
            var reportText = File.ReadAllText(Path.Combine(context.OutputPath, "reports", "tool-family-validation-compute.txt"));

            Assert.True(result.Success);
            Assert.Contains(result.Warnings, warning => warning.Contains("example prompt header is Examples:", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, warning => warning.Contains("missing 'resource group name'", StringComparison.Ordinal));
            Assert.Contains(result.Warnings, warning => warning.Contains("Branding: Use \"this tool\" instead of \"this command\".", StringComparison.Ordinal));
            Assert.Contains("Required params in prompts:", reportText, StringComparison.Ordinal);
            Assert.Contains("⚠️ 0/1 tools have all required params in examples", reportText, StringComparison.Ordinal);
            Assert.DoesNotContain("RESULT: FAIL", reportText, StringComparison.Ordinal);
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

    private static string ArticleWithToneMarkerPhrase()
        => """
        ---
        title: Compute tools
        tool_count: 1
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        You can list virtual machines by resource group and location.
        Example prompts include:
        - List virtual machines in resource group 'rg-one' for location 'eastus'
        | Parameter | Required |
        | --- | --- |
        | resource group | Yes |
        | location | No |

        ## Related content
        - Link
        """;

    private static string ArticleWithToneMarkerOnlyInTableRow()
        => """
        ---
        title: Compute tools
        tool_count: 1
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        Example prompts include:
        - List virtual machines in resource group 'rg-one' for location 'eastus'
        | Note | Guidance |
        | --- | --- |
        | example | you can use this row for formatting only |
        | Parameter | Required |
        | --- | --- |
        | resource group | Yes |
        | location | No |

        ## Related content
        - Link
        """;

    private static string ArticleWithToneMarkerInBulletItem()
        => """
        ---
        title: Compute tools
        tool_count: 1
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        Example prompts include:
        - you can list VMs by resource group
        - List virtual machines where resource group name is 'rg-one' in location 'eastus'
        | Parameter | Required |
        | --- | --- |
        | resource group name | Yes |
        | location | No |

        ## Related content
        - Link
        """;

    private static string ArticleWithToneMarkerOnlyInFencedCodeBlock()
        => """
        ---
        title: Compute tools
        tool_count: 1
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        ```text
        you can use this example as a placeholder inside the code block
        ```
        Example prompts include:
        - List virtual machines in resource group 'rg-one' for location 'eastus'
        | Parameter | Required |
        | --- | --- |
        | resource group | Yes |
        | location | No |

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
        - List resources where resource group name is 'rg-one' in location 'eastus'
        | Parameter | Required |
        | --- | --- |
        | resource group name | Yes |
        | location | No |

        ## Show virtual machine
        <!-- @mcpcli compute show -->
        Example prompts include:
        - Show the VM named 'vm-one' in resource group 'rg-one'
        | Parameter | Required |
        | --- | --- |
        | vm name | Yes |
        | resource group name | No |

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

    private static string ArticleWithExternalRelatedToolReferences()
        => """
        ---
        title: Compute tools
        tool_count: 1
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        Example prompts include:
        - List resources where resource group name is 'rg-one' in location 'eastus'
        | Parameter | Required |
        | --- | --- |
        | resource group name | Yes |
        | location | No |

        ## Related content
        - For cross-namespace workflows, see `monitor query` and `az vm list`.
        """;

    private static string ArticleWithInternalRelatedToolMissingH2()
        => """
        ---
        title: Compute tools
        tool_count: 2
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute compute-list -->
        Example prompts include:
        - List virtual machines in resource group 'rg-one' for location 'eastus'
        | Parameter | Required |
        | --- | --- |
        | resource group | Yes |
        | location | No |

        ## Related content
        - Use `compute-show` when you need details for a single virtual machine.
        """;

    private static string ArticleWithSpaceFormInternalRelatedToolMissingH2()
        => """
        ---
        title: Compute tools
        tool_count: 2
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        Example prompts include:
        - List virtual machines in resource group 'rg-one' for location 'eastus'
        | Parameter | Required |
        | --- | --- |
        | resource group | Yes |
        | location | No |

        ## Related content
        - Use `compute show` when you need details for a single virtual machine.
        """;

    private static string ArticleWithInternalRelatedToolMatchingH2()
        => """
        ---
        title: Compute tools
        tool_count: 2
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute compute-list -->
        Example prompts include:
        - List virtual machines where resource group name is 'rg-one'
        | Parameter | Required |
        | --- | --- |
        | resource group name | Yes |
        | location | No |

        ## Show virtual machine
        <!-- @mcpcli compute compute-show -->
        Example prompts include:
        - Show the virtual machine where vm name is 'vm-one' in resource group name 'rg-one'
        | Parameter | Required |
        | --- | --- |
        | vm name | Yes |
        | resource group name | No |

        ## Related content
        - Use `compute-show` when you need details for a single virtual machine.
        """;

    private static string ArticleWithoutRelatedSection()
        => """
        ---
        title: Compute tools
        tool_count: 1
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        Example prompts include:
        - List virtual machines in resource group 'rg-one' for location 'eastus'
        | Parameter | Required |
        | --- | --- |
        | resource group | Yes |
        | location | No |
        """;

    private static string ArticleWithMissingExampleHeader()
        => """
        ---
        title: Compute tools
        tool_count: 1
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        Use this tool to list virtual machines by resource group and location.
        | Parameter | Required |
        | --- | --- |
        | resource group | Yes |
        | location | No |

        ## Related content
        - Link
        """;

    private static string ArticleWithAlternateExampleHeader()
        => """
        ---
        title: Compute tools
        tool_count: 1
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        Examples:
        - List virtual machines where resource group name is 'rg-one' in location 'eastus'
        | Parameter | Required |
        | --- | --- |
        | resource group name | Yes |
        | location | No |

        ## Related content
        - Link
        """;

    private static string ArticleWithOneParameter()
        => """
        ---
        title: Compute tools
        tool_count: 1
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        Example prompts include:
        - List virtual machines where resource group name is 'rg-one'
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

    [Fact]
    public async Task ValidateAsync_GroupedArticleWithH2CategoriesAndH3Tools_CountsMarkers()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-show.md"), "compute show");
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-delete.md"), "compute delete");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), GroupedArticleWithCategories());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            if (!result.Success)
            {
                var warnings = string.Join("\n", result.Warnings);
                throw new Exception($"Validation failed with warnings:\n{warnings}");
            }

            Assert.True(result.Success);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    [Fact]
    public async Task ValidateAsync_GroupedArticleWithCorrectMarkerCount_PassesValidation()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-show.md"), "compute show");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), GroupedArticleWithCorrectCount());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            if (!result.Success)
            {
                var warnings = string.Join("\n", result.Warnings);
                throw new Exception($"Validation failed with warnings:\n{warnings}");
            }

            Assert.True(result.Success);
            Assert.Empty(result.Warnings);
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static string GroupedArticleWithCategories()
        => """
        ---
        title: Compute tools
        tool_count: 3
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        Example prompts include:
        - List all virtual machines in resource group 'rg-one'
        | Parameter | Required |
        | --- | --- |
        | subscription | No |
        | resource group | No |

        ## Show virtual machine
        <!-- @mcpcli compute show -->
        Example prompts include:
        - Show the VM named 'vm-one' in resource group 'rg-one'
        | Parameter | Required |
        | --- | --- |
        | vm name | Yes |
        | resource group | No |

        ## Delete virtual machine
        <!-- @mcpcli compute delete -->
        Example prompts include:
        - Delete the VM named 'vm-test' in resource group 'rg-dev'
        | Parameter | Required |
        | --- | --- |
        | vm name | Yes |
        | resource group | Yes |

        ## Related content
        - Link
        """;

    private static string GroupedArticleWithCorrectCount()
        => """
        ---
        title: Compute tools
        tool_count: 2
        ---
        # Compute tools

        ## List virtual machines
        <!-- @mcpcli compute list -->
        Example prompts include:
        - List all virtual machines in resource group 'rg-one'
        | Parameter | Required |
        | --- | --- |
        | subscription | No |
        | resource group | No |

        ## Show virtual machine
        <!-- @mcpcli compute show -->
        Example prompts include:
        - Show the VM named 'vm-one' in resource group 'rg-one'
        | Parameter | Required |
        | --- | --- |
        | vm name | Yes |
        | resource group | No |

        ## Related content
        - Link
        """;

    [Fact]
    public async Task ValidateAsync_MissingToolFromArticle_ListsToolNameInError()
    {
        var testRoot = CreateTestRoot();
        try
        {
            var context = CreateContext(testRoot);
            // 3 tool files but article only has 2 H2 sections
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-list.md"), "compute list");
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-show.md"), "compute show");
            SeedToolFile(Path.Combine(context.OutputPath, "tools", "compute-create.md"), "compute create");
            SeedFile(Path.Combine(context.OutputPath, "tool-family", "compute.md"), ArticleWithMissingTool());

            var validator = new ToolFamilyPostAssemblyValidator();
            var result = await validator.ValidateAsync(context, new FakeStep(), CancellationToken.None);

            Assert.False(result.Success);
            // The error should name the missing tool file
            Assert.Contains(result.Warnings, warning =>
                warning.Contains("Cross-reference check failed", StringComparison.Ordinal)
                && warning.Contains("compute-create.md", StringComparison.Ordinal)
                && warning.Contains("Regenerate the namespace to include them", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTestRoot(testRoot);
        }
    }

    private static string ArticleWithMissingTool()
        => """
        ---
        title: Compute tools
        tool_count: 3
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
