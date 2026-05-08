// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace Shared.Tests;

public class CliToolInfoTests
{
    [Fact]
    public void Construction_RequiredParameters_SetsCorrectly()
    {
        var switches = new List<CliSwitch>
        {
            new("--sub", "subscription", "string")
        };

        var tool = new CliToolInfo(
            Command: "storage account list",
            Description: "List storage accounts",
            Switches: switches,
            IsDestructive: true,
            IsReadOnly: false,
            EnrichmentMatched: true);

        Assert.Equal("storage account list", tool.Command);
        Assert.Equal("List storage accounts", tool.Description);
        Assert.Single(tool.Switches);
        Assert.True(tool.IsDestructive);
        Assert.False(tool.IsReadOnly);
        Assert.True(tool.EnrichmentMatched);
    }

    [Fact]
    public void Construction_DefaultValues_AppliedCorrectly()
    {
        var tool = new CliToolInfo("cmd", "desc", Array.Empty<CliSwitch>());

        Assert.False(tool.IsDestructive);
        Assert.False(tool.IsReadOnly);
        Assert.Null(tool.EnrichmentMatched);
    }

    [Fact]
    public void Construction_EmptySwitchesList_Allowed()
    {
        var tool = new CliToolInfo("cmd", "desc", new List<CliSwitch>());

        Assert.Empty(tool.Switches);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var switches = Array.Empty<CliSwitch>();
        var a = new CliToolInfo("cmd", "desc", switches);
        var b = new CliToolInfo("cmd", "desc", switches);

        Assert.Equal(a, b);
    }
}
