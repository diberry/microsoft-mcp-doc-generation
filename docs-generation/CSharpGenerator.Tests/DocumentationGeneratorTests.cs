// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using CSharpGenerator.Models;
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Tests DocumentationGenerator helper methods: ParseCommand, ConvertCamelCaseToTitleCase,
/// ExtractConditionalRequirement, ExtractCommonParameters, TransformCliOutput, MergeCommonParameters.
/// Priority: P0/P1 — these are core transformation logic.
/// </summary>
[Collection("StaticState")]
public class DocumentationGeneratorTests
{
    // ── ParseCommand ───────────────────────────────────────────

    [Theory]
    [InlineData("storage account list", "storage account", "list")]
    [InlineData("storage account get", "storage account", "get")]
    [InlineData("aks cluster get", "aks cluster", "get")]
    [InlineData("subscription list", "subscription", "list")]
    [InlineData("keyvault secret get", "keyvault secret", "get")]
    public void ParseCommand_SplitsCorrectly(string command, string expectedFamily, string expectedOp)
    {
        var (family, op) = DocumentationGenerator.ParseCommand(command);
        Assert.Equal(expectedFamily, family);
        Assert.Equal(expectedOp, op);
    }

    [Fact]
    public void ParseCommand_FourPartCommand_TakesLastAsOperation()
    {
        var (family, op) = DocumentationGenerator.ParseCommand("storage blob container list");
        Assert.Equal("storage blob container", family);
        Assert.Equal("list", op);
    }

    [Fact]
    public void ParseCommand_EmptyString_ReturnsBothEmpty()
    {
        var (family, op) = DocumentationGenerator.ParseCommand("");
        Assert.Equal("", family);
        Assert.Equal("", op);
    }

    [Fact]
    public void ParseCommand_Null_ReturnsBothEmpty()
    {
        var (family, op) = DocumentationGenerator.ParseCommand(null!);
        Assert.Equal("", family);
        Assert.Equal("", op);
    }

    [Fact]
    public void ParseCommand_SingleWord_ReturnsBothEmpty()
    {
        var (family, op) = DocumentationGenerator.ParseCommand("storage");
        Assert.Equal("", family);
        Assert.Equal("", op);
    }

    // ── ConvertCamelCaseToTitleCase ─────────────────────────────

    [Theory]
    [InlineData("openWorld", "Open World")]
    [InlineData("readOnly", "Read Only")]
    [InlineData("destructive", "Destructive")]
    [InlineData("idempotent", "Idempotent")]
    [InlineData("localRequired", "Local Required")]
    [InlineData("secret", "Secret")]
    public void ConvertCamelCaseToTitleCase_TransformsCorrectly(string input, string expected)
    {
        var result = DocumentationGenerator.ConvertCamelCaseToTitleCase(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertCamelCaseToTitleCase_EmptyString_ReturnsEmpty()
    {
        var result = DocumentationGenerator.ConvertCamelCaseToTitleCase("");
        Assert.Equal("", result);
    }

    [Fact]
    public void ConvertCamelCaseToTitleCase_Null_ReturnsNull()
    {
        var result = DocumentationGenerator.ConvertCamelCaseToTitleCase(null!);
        Assert.Null(result);
    }

    [Fact]
    public void ConvertCamelCaseToTitleCase_SingleChar_UpperCased()
    {
        var result = DocumentationGenerator.ConvertCamelCaseToTitleCase("a");
        Assert.Equal("A", result);
    }

    // ── ExtractConditionalRequirement ──────────────────────────

    [Fact]
    public void ExtractConditionalRequirement_WithConditional_ExtractsNote()
    {
        var desc = "Get a specific storage account. Requires at least one of --account-name or --account-id.";
        var (note, parameters) = DocumentationGenerator.ExtractConditionalRequirement(desc);

        Assert.NotNull(note);
        Assert.Contains("Requires at least one", note);
    }

    [Fact]
    public void ExtractConditionalRequirement_WithConditional_ExtractsParameters()
    {
        var desc = "Get a secret. Requires at least one of --vault-name or --secret-id.";
        var (_, parameters) = DocumentationGenerator.ExtractConditionalRequirement(desc);

        Assert.Equal(2, parameters.Count);
        Assert.Contains("--vault-name", parameters);
        Assert.Contains("--secret-id", parameters);
    }

    [Fact]
    public void ExtractConditionalRequirement_NoConditional_ReturnsEmpty()
    {
        var desc = "List all storage accounts in a subscription.";
        var (note, parameters) = DocumentationGenerator.ExtractConditionalRequirement(desc);

        Assert.Null(note);
        Assert.Empty(parameters);
    }

    [Fact]
    public void ExtractConditionalRequirement_EmptyString_ReturnsEmpty()
    {
        var (note, parameters) = DocumentationGenerator.ExtractConditionalRequirement("");
        Assert.Null(note);
        Assert.Empty(parameters);
    }

    [Fact]
    public void ExtractConditionalRequirement_NullString_ReturnsEmpty()
    {
        var (note, parameters) = DocumentationGenerator.ExtractConditionalRequirement(null!);
        Assert.Null(note);
        Assert.Empty(parameters);
    }

    [Fact]
    public void ExtractConditionalRequirement_MultipleParams_DeduplicatesNames()
    {
        var desc = "Requires at least one of --name, --id, or --name.";
        var (_, parameters) = DocumentationGenerator.ExtractConditionalRequirement(desc);

        // --name appears twice in text but should be deduplicated
        Assert.Equal(2, parameters.Count);
        Assert.Contains("--name", parameters);
        Assert.Contains("--id", parameters);
    }

    // ── ExtractCommonParameters ────────────────────────────────

    [Fact]
    public void ExtractCommonParameters_FindsParamsAbove50Percent()
    {
        // 5 tools, all have --subscription and --tenant → 100%
        var cliOutput = TestHelpers.LoadCliOutput();
        var tools = cliOutput.Results;

        var common = DocumentationGenerator.ExtractCommonParameters(tools);

        Assert.Contains(common, p => p.Name == "--subscription");
        Assert.Contains(common, p => p.Name == "--tenant");
        Assert.Contains(common, p => p.Name == "--auth-method");
    }

    [Fact]
    public void ExtractCommonParameters_ExcludesParamsBelow50Percent()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        var tools = cliOutput.Results;

        var common = DocumentationGenerator.ExtractCommonParameters(tools);

        // --container-name only appears in 1 of 5 tools (20%) → below threshold
        Assert.DoesNotContain(common, p => p.Name == "--container-name");
        // --blob-name only appears in 1 of 5 tools (20%)
        Assert.DoesNotContain(common, p => p.Name == "--blob-name");
    }

    [Fact]
    public void ExtractCommonParameters_SortedByUsageThenName()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        var common = DocumentationGenerator.ExtractCommonParameters(cliOutput.Results);

        // Verify sorted by usage desc, then name asc
        for (int i = 1; i < common.Count; i++)
        {
            if (common[i].UsagePercent == common[i - 1].UsagePercent)
            {
                Assert.True(
                    string.Compare(common[i - 1].Name, common[i].Name, StringComparison.Ordinal) <= 0,
                    $"Parameters with same usage should be sorted by name: {common[i - 1].Name} vs {common[i].Name}");
            }
            else
            {
                Assert.True(
                    common[i - 1].UsagePercent >= common[i].UsagePercent,
                    "Parameters should be sorted by usage descending");
            }
        }
    }

    [Fact]
    public void ExtractCommonParameters_EmptyToolList_ReturnsEmpty()
    {
        var common = DocumentationGenerator.ExtractCommonParameters(new List<Tool>());
        Assert.Empty(common);
    }

    [Fact]
    public void ExtractCommonParameters_UsagePercentCalculation()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        var common = DocumentationGenerator.ExtractCommonParameters(cliOutput.Results);

        // --subscription appears in all 5 tools → 100%
        var sub = common.First(p => p.Name == "--subscription");
        Assert.Equal(100.0, sub.UsagePercent);
    }

    // ── TransformCliOutput ─────────────────────────────────────

    [Fact]
    public void TransformCliOutput_GroupsByArea()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        var transformed = DocumentationGenerator.TransformCliOutput(cliOutput);

        Assert.Equal(2, transformed.Areas.Count);
        Assert.True(transformed.Areas.ContainsKey("storage"));
        Assert.True(transformed.Areas.ContainsKey("keyvault"));
    }

    [Fact]
    public void TransformCliOutput_AreaToolCount()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        var transformed = DocumentationGenerator.TransformCliOutput(cliOutput);

        Assert.Equal(3, transformed.Areas["storage"].ToolCount);
        Assert.Equal(2, transformed.Areas["keyvault"].ToolCount);
    }

    [Fact]
    public void TransformCliOutput_SetsAreaOnTools()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        var transformed = DocumentationGenerator.TransformCliOutput(cliOutput);

        Assert.All(transformed.Areas["storage"].Tools, t => Assert.Equal("storage", t.Area));
        Assert.All(transformed.Areas["keyvault"].Tools, t => Assert.Equal("keyvault", t.Area));
    }

    [Fact]
    public void TransformCliOutput_SetsVersionTo1_0_0()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        var transformed = DocumentationGenerator.TransformCliOutput(cliOutput);

        Assert.Equal("1.0.0", transformed.Version);
    }

    [Fact]
    public void TransformCliOutput_SetsGeneratedAtToUtcNow()
    {
        var before = DateTime.UtcNow;
        var cliOutput = TestHelpers.LoadCliOutput();
        var transformed = DocumentationGenerator.TransformCliOutput(cliOutput);
        var after = DateTime.UtcNow;

        Assert.InRange(transformed.GeneratedAt, before, after);
    }

    [Fact]
    public void TransformCliOutput_ToolsListContainsAll()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        var transformed = DocumentationGenerator.TransformCliOutput(cliOutput);

        Assert.Equal(5, transformed.Tools.Count);
    }

    [Fact]
    public void TransformCliOutput_SortsParametersRequiredFirst()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        var transformed = DocumentationGenerator.TransformCliOutput(cliOutput);

        // "storage blob upload" has 4 required + 3 optional
        var uploadTool = transformed.Tools.First(t => t.Command == "storage blob upload");
        var requiredCount = uploadTool.Option!.TakeWhile(o => o.Required).Count();
        var optionalStart = uploadTool.Option!.Skip(requiredCount).All(o => !o.Required);

        Assert.True(requiredCount > 0);
        Assert.True(optionalStart);
    }

    [Fact]
    public void TransformCliOutput_ExtractsConditionalRequirement()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        var transformed = DocumentationGenerator.TransformCliOutput(cliOutput);

        var tool = transformed.Tools.First(t => t.Command == "storage account get");
        Assert.True(tool.HasConditionalRequired);
        Assert.NotNull(tool.ConditionalRequiredNote);
        Assert.Contains("--account-name", tool.ConditionalRequiredParameters!);
        Assert.Contains("--account-id", tool.ConditionalRequiredParameters!);
    }

    [Fact]
    public void TransformCliOutput_NonConditionalTool_HasConditionalIsFalse()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        var transformed = DocumentationGenerator.TransformCliOutput(cliOutput);

        var tool = transformed.Tools.First(t => t.Command == "storage account list");
        Assert.False(tool.HasConditionalRequired);
        Assert.Null(tool.ConditionalRequiredNote);
    }

    [Fact]
    public void TransformCliOutput_SkipsToolWithEmptyCommand()
    {
        var cliOutput = TestHelpers.CreateCliOutput(
            TestHelpers.CreateTool("storage list"),
            new Tool { Command = "", Description = "Empty command" },
            new Tool { Command = null, Description = "Null command" }
        );

        var transformed = DocumentationGenerator.TransformCliOutput(cliOutput);

        // Tools with empty/null Command are still in the Tools list (not removed),
        // but they are NOT grouped into any area and don't get Area set.
        Assert.Equal(3, transformed.Tools.Count);
        Assert.Single(transformed.Areas); // only "storage"
        Assert.True(transformed.Areas.ContainsKey("storage"));
        Assert.Single(transformed.Tools.Where(t => t.Area != null));
    }

    [Fact]
    public void TransformCliOutput_AreaDescription_MatchesPattern()
    {
        var cliOutput = TestHelpers.LoadCliOutput();
        var transformed = DocumentationGenerator.TransformCliOutput(cliOutput);

        Assert.Equal("storage area tools", transformed.Areas["storage"].Description);
        Assert.Equal("keyvault area tools", transformed.Areas["keyvault"].Description);
    }

    // ── MergeCommonParameters ──────────────────────────────────

    [Fact]
    public void MergeCommonParameters_SourceOverridesCli()
    {
        var data = new TransformedData
        {
            Tools = new List<Tool>
            {
                TestHelpers.CreateTool("test list", options: new List<Option>
                {
                    TestHelpers.CreateOption("--param-a"),
                    TestHelpers.CreateOption("--param-b"),
                }),
                TestHelpers.CreateTool("test get", options: new List<Option>
                {
                    TestHelpers.CreateOption("--param-a"),
                    TestHelpers.CreateOption("--param-b"),
                })
            }
        };

        var sourceParams = new List<CommonParameter>
        {
            new CommonParameter { Name = "--param-a", Description = "Source description", Type = "string" }
        };

        var merged = DocumentationGenerator.MergeCommonParameters(data, sourceParams);
        var paramA = merged.SourceDiscoveredCommonParams.First(p => p.Name == "--param-a");

        Assert.Equal("Source description", paramA.Description);
    }

    [Fact]
    public void MergeCommonParameters_ResultSortedByName()
    {
        var data = new TransformedData
        {
            Tools = new List<Tool>
            {
                TestHelpers.CreateTool("t1", options: new List<Option>
                {
                    TestHelpers.CreateOption("--zebra"),
                    TestHelpers.CreateOption("--alpha"),
                }),
                TestHelpers.CreateTool("t2", options: new List<Option>
                {
                    TestHelpers.CreateOption("--zebra"),
                    TestHelpers.CreateOption("--alpha"),
                })
            }
        };

        var merged = DocumentationGenerator.MergeCommonParameters(data, new List<CommonParameter>());

        for (int i = 1; i < merged.SourceDiscoveredCommonParams.Count; i++)
        {
            Assert.True(
                string.Compare(
                    merged.SourceDiscoveredCommonParams[i - 1].Name,
                    merged.SourceDiscoveredCommonParams[i].Name,
                    StringComparison.Ordinal) <= 0);
        }
    }
}
