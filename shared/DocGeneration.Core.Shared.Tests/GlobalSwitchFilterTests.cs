// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace Shared.Tests;

public class GlobalSwitchFilterTests
{
    // ── IsGlobalSwitch ────────────────────────────────────────────

    [Theory]
    [InlineData("--subscription")]
    [InlineData("--tenant")]
    [InlineData("--tenant-id")]
    [InlineData("--auth-method")]
    [InlineData("--retry-delay")]
    [InlineData("--retry-max-delay")]
    [InlineData("--retry-max-retries")]
    [InlineData("--retry-mode")]
    [InlineData("--retry-network-timeout")]
    [InlineData("--learn")]
    public void IsGlobalSwitch_KnownGlobalSwitches_ReturnsTrue(string switchName)
    {
        Assert.True(GlobalSwitchFilter.IsGlobalSwitch(switchName));
    }

    [Theory]
    [InlineData("--resource-group")]
    [InlineData("--name")]
    [InlineData("--location")]
    [InlineData("--sku")]
    [InlineData("--output")]
    [InlineData("--query")]
    [InlineData("--verbose")]
    public void IsGlobalSwitch_NonGlobalSwitches_ReturnsFalse(string switchName)
    {
        Assert.False(GlobalSwitchFilter.IsGlobalSwitch(switchName));
    }

    [Theory]
    [InlineData("--Subscription")]
    [InlineData("--TENANT")]
    [InlineData("--Auth-Method")]
    [InlineData("--RETRY-DELAY")]
    public void IsGlobalSwitch_CaseSensitive_ReturnsFalse(string switchName)
    {
        // GlobalSwitchFilter uses exact string matching (case-sensitive)
        Assert.False(GlobalSwitchFilter.IsGlobalSwitch(switchName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("--unknown")]
    public void IsGlobalSwitch_EmptyOrUnknown_ReturnsFalse(string switchName)
    {
        Assert.False(GlobalSwitchFilter.IsGlobalSwitch(switchName));
    }

    // ── FilterOutGlobal ───────────────────────────────────────────

    [Fact]
    public void FilterOutGlobal_RemovesGlobalSwitches()
    {
        var switches = new List<CliSwitch>
        {
            new("--subscription", "Azure subscription"),
            new("--resource-group", "Resource group name"),
            new("--tenant", "Tenant ID"),
            new("--name", "Resource name"),
            new("--auth-method", "Auth method")
        };

        var result = GlobalSwitchFilter.FilterOutGlobal(switches);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, s => s.Name == "--resource-group");
        Assert.Contains(result, s => s.Name == "--name");
        Assert.DoesNotContain(result, s => s.Name == "--subscription");
        Assert.DoesNotContain(result, s => s.Name == "--tenant");
        Assert.DoesNotContain(result, s => s.Name == "--auth-method");
    }

    [Fact]
    public void FilterOutGlobal_AllNonGlobalSwitches_ReturnsAll()
    {
        var switches = new List<CliSwitch>
        {
            new("--resource-group", "Resource group"),
            new("--location", "Location"),
            new("--name", "Name")
        };

        var result = GlobalSwitchFilter.FilterOutGlobal(switches);

        Assert.Equal(3, result.Count);
        Assert.Equal(switches, result);
    }

    [Fact]
    public void FilterOutGlobal_AllGlobalSwitches_ReturnsEmpty()
    {
        var switches = new List<CliSwitch>
        {
            new("--subscription", "Subscription"),
            new("--tenant", "Tenant"),
            new("--retry-delay", "Retry delay")
        };

        var result = GlobalSwitchFilter.FilterOutGlobal(switches);

        Assert.Empty(result);
    }

    [Fact]
    public void FilterOutGlobal_EmptyInput_ReturnsEmpty()
    {
        var switches = new List<CliSwitch>();

        var result = GlobalSwitchFilter.FilterOutGlobal(switches);

        Assert.Empty(result);
    }

    [Fact]
    public void FilterOutGlobal_ReturnsReadOnlyList()
    {
        var switches = new List<CliSwitch>
        {
            new("--name", "Name")
        };

        var result = GlobalSwitchFilter.FilterOutGlobal(switches);

        Assert.IsAssignableFrom<IReadOnlyList<CliSwitch>>(result);
    }

    [Fact]
    public void FilterOutGlobal_PreservesOrder()
    {
        var switches = new List<CliSwitch>
        {
            new("--name", "Name"),
            new("--subscription", "Sub"),
            new("--location", "Location"),
            new("--tenant", "Tenant"),
            new("--sku", "SKU")
        };

        var result = GlobalSwitchFilter.FilterOutGlobal(switches);

        Assert.Equal(3, result.Count);
        Assert.Equal("--name", result[0].Name);
        Assert.Equal("--location", result[1].Name);
        Assert.Equal("--sku", result[2].Name);
    }

    [Fact]
    public void FilterOutGlobal_RetryParameters_AllFiltered()
    {
        var switches = new List<CliSwitch>
        {
            new("--retry-delay", "Retry delay"),
            new("--retry-max-delay", "Max delay"),
            new("--retry-max-retries", "Max retries"),
            new("--retry-mode", "Retry mode"),
            new("--retry-network-timeout", "Network timeout"),
            new("--name", "Resource name")
        };

        var result = GlobalSwitchFilter.FilterOutGlobal(switches);

        Assert.Single(result);
        Assert.Equal("--name", result[0].Name);
    }
}
