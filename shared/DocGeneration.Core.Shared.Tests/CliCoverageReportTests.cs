// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace Shared.Tests;

public class CliCoverageReportTests
{
    [Fact]
    public void CliDataCoverage_CalculatesCorrectly()
    {
        var report = new CliCoverageReport
        {
            TotalTools = 10,
            ToolsWithCliData = 7
        };
        Assert.Equal(70.0, report.CliDataCoverage);
    }

    [Fact]
    public void CliTabCoverage_CalculatesCorrectly()
    {
        var report = new CliCoverageReport
        {
            TotalTools = 20,
            ToolsWithCliTabs = 15
        };
        Assert.Equal(75.0, report.CliTabCoverage);
    }

    [Fact]
    public void AiImprovementRate_CalculatesCorrectly()
    {
        var report = new CliCoverageReport
        {
            TotalTools = 10,
            ToolsWithCliData = 8,
            ToolsWithAiImprovedProse = 6
        };
        Assert.Equal(75.0, report.AiImprovementRate);
    }

    [Fact]
    public void AiViolationRate_CalculatesCorrectly()
    {
        var report = new CliCoverageReport
        {
            TotalTools = 10,
            ToolsWithCliData = 5,
            AiValidationFailures = 1
        };
        Assert.Equal(20.0, report.AiViolationRate);
    }

    [Fact]
    public void ZeroTotalTools_ReturnsZeroPercent()
    {
        var report = new CliCoverageReport
        {
            TotalTools = 0,
            ToolsWithCliData = 0,
            ToolsWithCliTabs = 0,
            ToolsWithAiImprovedProse = 0,
            AiValidationFailures = 0
        };
        Assert.Equal(0.0, report.CliDataCoverage);
        Assert.Equal(0.0, report.CliTabCoverage);
        Assert.Equal(0.0, report.AiImprovementRate);
        Assert.Equal(0.0, report.AiViolationRate);
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var report = new CliCoverageReport
        {
            Namespace = "azure-storage",
            TotalTools = 10,
            ToolsWithCliData = 8,
            ToolsWithCliTabs = 6,
            ToolsWithAiImprovedProse = 7,
            AiValidationFailures = 1
        };

        var result = report.ToString();
        Assert.Contains("[azure-storage]", result);
        Assert.Contains("CLI: 8/10 (80%)", result);
        Assert.Contains("Tabs: 6/10 (60%)", result);
        Assert.Contains("AI: 7 improved", result);
        Assert.Contains("1 failures", result);
    }
}
