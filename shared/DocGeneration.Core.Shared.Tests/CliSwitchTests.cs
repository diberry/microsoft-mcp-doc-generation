// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace Shared.Tests;

public class CliSwitchTests
{
    [Fact]
    public void Construction_AllParameters_SetsCorrectly()
    {
        var allowed = new List<string> { "val1", "val2" };
        var sw = new CliSwitch(
            Name: "--subscription",
            Description: "Azure subscription ID",
            Type: "string",
            IsRequired: true,
            Default: "default-sub",
            ShortAlias: "-s",
            ValuePlaceholder: "<sub-id>",
            AllowedValues: allowed);

        Assert.Equal("--subscription", sw.Name);
        Assert.Equal("Azure subscription ID", sw.Description);
        Assert.Equal("string", sw.Type);
        Assert.True(sw.IsRequired);
        Assert.Equal("default-sub", sw.Default);
        Assert.Equal("-s", sw.ShortAlias);
        Assert.Equal("<sub-id>", sw.ValuePlaceholder);
        Assert.Equal(new[] { "val1", "val2" }, sw.AllowedValues);
    }

    [Fact]
    public void Construction_DefaultValues_AppliedCorrectly()
    {
        var sw = new CliSwitch("--name", "desc");

        Assert.Equal("string", sw.Type);
        Assert.Null(sw.IsRequired);
        Assert.Null(sw.Default);
        Assert.Null(sw.ShortAlias);
        Assert.Null(sw.ValuePlaceholder);
        Assert.Null(sw.AllowedValues);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new CliSwitch("--name", "desc", "string");
        var b = new CliSwitch("--name", "desc", "string");

        Assert.Equal(a, b);
    }

    [Fact]
    public void WithExpression_ProducesNewInstance()
    {
        var original = new CliSwitch("--name", "desc");
        var modified = original with { Default = "new-default" };

        Assert.Null(original.Default);
        Assert.Equal("new-default", modified.Default);
        Assert.NotSame(original, modified);
    }

    // ── Boundary: empty/whitespace fields ────────────────────────────

    [Fact]
    public void Construction_EmptyName_Allowed()
    {
        var sw = new CliSwitch("", "desc");
        Assert.Equal("", sw.Name);
    }

    [Fact]
    public void Construction_WhitespaceName_Allowed()
    {
        var sw = new CliSwitch("  ", "desc");
        Assert.Equal("  ", sw.Name);
    }

    [Fact]
    public void Construction_EmptyDescription_Allowed()
    {
        var sw = new CliSwitch("--flag", "");
        Assert.Equal("", sw.Description);
    }

    [Fact]
    public void Construction_WhitespaceDescription_Allowed()
    {
        var sw = new CliSwitch("--flag", "   ");
        Assert.Equal("   ", sw.Description);
    }

    [Fact]
    public void Construction_EmptyType_Allowed()
    {
        var sw = new CliSwitch("--flag", "desc", "");
        Assert.Equal("", sw.Type);
    }
}
