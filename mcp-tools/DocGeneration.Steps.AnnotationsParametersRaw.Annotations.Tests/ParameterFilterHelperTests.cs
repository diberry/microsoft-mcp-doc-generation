// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CSharpGenerator.Generators;
using CSharpGenerator.Models;
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Tests ParameterFilterHelper.ShouldInclude - the shared predicate that decides
/// whether a parameter appears in tool-specific parameter tables and counts.
/// </summary>
public class ParameterFilterHelperTests
{
    [Fact]
    public void ShouldInclude_RequiredResourceGroup_InCommon_ReturnsTrue()
    {
        var common = MakeCommonSet("--resource-group", "--subscription", "--tenant");
        var opt = MakeOption("--resource-group", required: true);
        Assert.True(ParameterFilterHelper.ShouldInclude(opt, common));
    }

    [Fact]
    public void ShouldInclude_OptionalResourceGroup_InCommon_ReturnsFalse()
    {
        var common = MakeCommonSet("--resource-group", "--subscription", "--tenant");
        var opt = MakeOption("--resource-group", required: false);
        Assert.False(ParameterFilterHelper.ShouldInclude(opt, common));
    }

    [Fact]
    public void ShouldInclude_RequiredResourceGroup_NotInCommon_ReturnsTrue()
    {
        var common = MakeCommonSet("--tenant", "--auth-method");
        var opt = MakeOption("--resource-group", required: true);
        Assert.True(ParameterFilterHelper.ShouldInclude(opt, common));
    }

    [Fact]
    public void ShouldInclude_OptionalResourceGroup_NotInCommon_ReturnsTrue()
    {
        var common = MakeCommonSet("--tenant", "--auth-method");
        var opt = MakeOption("--resource-group", required: false);
        Assert.True(ParameterFilterHelper.ShouldInclude(opt, common));
    }

    [Fact]
    public void ShouldInclude_RequiredSubscription_InCommon_ReturnsTrue()
    {
        var common = MakeCommonSet("--resource-group", "--subscription", "--tenant");
        var opt = MakeOption("--subscription", required: true);
        Assert.True(ParameterFilterHelper.ShouldInclude(opt, common));
    }

    [Fact]
    public void ShouldInclude_OptionalSubscription_InCommon_ReturnsFalse()
    {
        var common = MakeCommonSet("--resource-group", "--subscription", "--tenant");
        var opt = MakeOption("--subscription", required: false);
        Assert.False(ParameterFilterHelper.ShouldInclude(opt, common));
    }

    [Fact]
    public void ShouldInclude_RequiredTenant_InCommon_ReturnsTrue()
    {
        var common = MakeCommonSet("--tenant", "--auth-method", "--retry-delay");
        var opt = MakeOption("--tenant", required: true);
        Assert.True(ParameterFilterHelper.ShouldInclude(opt, common));
    }

    [Fact]
    public void ShouldInclude_OptionalTenant_InCommon_ReturnsFalse()
    {
        var common = MakeCommonSet("--tenant", "--auth-method", "--retry-delay");
        var opt = MakeOption("--tenant", required: false);
        Assert.False(ParameterFilterHelper.ShouldInclude(opt, common));
    }

    [Fact]
    public void ShouldInclude_OptionalRetryDelay_InCommon_ReturnsFalse()
    {
        var common = MakeCommonSet("--tenant", "--auth-method", "--retry-delay");
        var opt = MakeOption("--retry-delay", required: false);
        Assert.False(ParameterFilterHelper.ShouldInclude(opt, common));
    }

    [Fact]
    public void ShouldInclude_ToolSpecificParam_AlwaysIncluded()
    {
        var common = MakeCommonSet("--tenant", "--auth-method");
        var opt = MakeOption("--server-name", required: true);
        Assert.True(ParameterFilterHelper.ShouldInclude(opt, common));
    }

    [Fact]
    public void ShouldInclude_OptionalToolSpecificParam_Included()
    {
        var common = MakeCommonSet("--tenant", "--auth-method");
        var opt = MakeOption("--filter", required: false);
        Assert.True(ParameterFilterHelper.ShouldInclude(opt, common));
    }

    [Fact]
    public void ShouldInclude_NullName_ReturnsFalse()
    {
        var common = MakeCommonSet("--tenant");
        var opt = new Option { Name = null, Required = true };
        Assert.False(ParameterFilterHelper.ShouldInclude(opt, common));
    }

    [Fact]
    public void ShouldInclude_EmptyName_ReturnsFalse()
    {
        var common = MakeCommonSet("--tenant");
        var opt = new Option { Name = "", Required = true };
        Assert.False(ParameterFilterHelper.ShouldInclude(opt, common));
    }

    [Fact]
    public void ShouldInclude_EmptyCommonSet_AllNonEmptyIncluded()
    {
        var common = MakeCommonSet();
        var opt = MakeOption("--resource-group", required: false);
        Assert.True(ParameterFilterHelper.ShouldInclude(opt, common));
    }

    private static HashSet<string> MakeCommonSet(params string[] names)
        => new(names, StringComparer.OrdinalIgnoreCase);

    private static Option MakeOption(string name, bool required)
        => new() { Name = name, Required = required, Type = "string", Description = $"The {name} value." };
}
