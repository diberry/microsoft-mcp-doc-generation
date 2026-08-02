// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CSharpGenerator.Generators;
using CSharpGenerator.Models;
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Tests that PageGenerator.FilterToolOptions and DocumentationGenerator.CountNonCommonParameters
/// now include all named parameters in output and summary counts.
/// </summary>
public class ScopingParamRequiredTests
{
    [Fact]
    public void FilterToolOptions_RequiredResourceGroup_InCommon_IsRetained()
    {
        var common = MakeCommonSet("--resource-group", "--subscription", "--tenant");
        var options = new List<Option>
        {
            MakeOption("--resource-group", required: true),
            MakeOption("--server-name", required: true),
            MakeOption("--tenant", required: false),
        };

        var filtered = PageGenerator.FilterToolOptions(options, common);

        Assert.Contains(filtered, o => o.Name == "--resource-group");
        Assert.Contains(filtered, o => o.Name == "--server-name");
        Assert.Contains(filtered, o => o.Name == "--tenant");
    }

    [Fact]
    public void FilterToolOptions_OptionalResourceGroup_InCommon_IsExcluded()
    {
        var common = MakeCommonSet("--resource-group", "--subscription", "--tenant");
        var options = new List<Option>
        {
            MakeOption("--resource-group", required: false),
            MakeOption("--server-name", required: true),
        };

        var filtered = PageGenerator.FilterToolOptions(options, common);

        Assert.Contains(filtered, o => o.Name == "--resource-group");
        Assert.Contains(filtered, o => o.Name == "--server-name");
    }

    [Fact]
    public void FilterToolOptions_RequiredSubscription_InCommon_IsRetained()
    {
        var common = MakeCommonSet("--resource-group", "--subscription", "--tenant");
        var options = new List<Option>
        {
            MakeOption("--subscription", required: true),
            MakeOption("--database-name", required: true),
        };

        var filtered = PageGenerator.FilterToolOptions(options, common);

        Assert.Contains(filtered, o => o.Name == "--subscription");
        Assert.Contains(filtered, o => o.Name == "--database-name");
    }

    [Fact]
    public void FilterToolOptions_MixedRequiredAndOptionalCommonParams()
    {
        var common = MakeCommonSet("--resource-group", "--subscription", "--tenant", "--retry-delay");
        var options = new List<Option>
        {
            MakeOption("--resource-group", required: true),
            MakeOption("--subscription", required: true),
            MakeOption("--server-name", required: true),
            MakeOption("--tenant", required: false),
            MakeOption("--retry-delay", required: false),
        };

        var filtered = PageGenerator.FilterToolOptions(options, common);

        Assert.Equal(5, filtered.Count);
        Assert.Contains(filtered, o => o.Name == "--resource-group");
        Assert.Contains(filtered, o => o.Name == "--subscription");
        Assert.Contains(filtered, o => o.Name == "--server-name");
        Assert.Contains(filtered, o => o.Name == "--tenant");
        Assert.Contains(filtered, o => o.Name == "--retry-delay");
    }

    [Fact]
    public void CountNonCommonParameters_ReturnsTotalNamedParameters()
    {
        var common = MakeCommonSet("--resource-group", "--subscription", "--tenant");
        var tool = new Tool
        {
            Command = "sql database list",
            Option = new List<Option>
            {
                MakeOption("--resource-group", required: true),
                MakeOption("--subscription", required: true),
                MakeOption("--server-name", required: true),
                MakeOption("--tenant", required: false),
            }
        };

        var count = DocumentationGenerator.CountNonCommonParameters(tool, common);

        Assert.Equal(4, count);
    }

    [Fact]
    public void CountNonCommonParameters_AllOptionalCommon_ReturnsTotalCount()
    {
        var common = MakeCommonSet("--tenant", "--auth-method", "--retry-delay");
        var tool = new Tool
        {
            Command = "subscription list",
            Option = new List<Option>
            {
                MakeOption("--tenant", required: false),
                MakeOption("--auth-method", required: false),
                MakeOption("--retry-delay", required: false),
            }
        };

        var count = DocumentationGenerator.CountNonCommonParameters(tool, common);

        Assert.Equal(3, count);
    }

    [Fact]
    public void CountNonCommonParameters_NullOptionList_ReturnsZero()
    {
        var common = MakeCommonSet("--tenant");
        var tool = new Tool { Command = "test list", Option = null };

        var count = DocumentationGenerator.CountNonCommonParameters(tool, common);

        Assert.Equal(0, count);
    }

    private static HashSet<string> MakeCommonSet(params string[] names)
        => new(names, StringComparer.OrdinalIgnoreCase);

    private static Option MakeOption(string name, bool required)
        => new() { Name = name, Required = required, Type = "string", Description = $"The {name} value." };
}
