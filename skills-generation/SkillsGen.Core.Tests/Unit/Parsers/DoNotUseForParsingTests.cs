using FluentAssertions;
using SkillsGen.Core.Parsers;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Parsers;

/// <summary>
/// Tests for GitHub Issue #443: DO NOT USE FOR delimiter parsing.
/// Verifies that "DO NOT USE FOR:" items in skill descriptions are correctly
/// separated from "WHEN:" items and routed to DoNotUseFor (not UseFor).
/// </summary>
public class DoNotUseForParsingTests
{
    private readonly SkillMarkdownParser _parser = new();

    private static string WrapDescription(string name, string description) =>
        $"---\nname: {name}\ndescription: \"{description}\"\n---\n\nBody content.\n";

    // ==========================================================================
    // Test 1: DO NOT USE FOR delimiter parsing (Issue #443 core fix)
    // ==========================================================================

    [Fact]
    public void Parse_DoNotUseFor_ItemsRouteToDoNotUseForList()
    {
        var desc = "Build copilot apps. WHEN: copilot SDK, deploy copilot app. "
                 + "DO NOT USE FOR: general web apps without copilot SDK (use azure-prepare), "
                 + "Copilot Extensions, Foundry agents (use microsoft-foundry).";
        var content = WrapDescription("azure-hosted-copilot-sdk", desc);

        var result = _parser.Parse("azure-hosted-copilot-sdk", content);

        // DO NOT USE FOR items must land in DoNotUseFor
        result.DoNotUseFor.Should().Contain(item =>
            item.Contains("general web apps", StringComparison.OrdinalIgnoreCase));
        result.DoNotUseFor.Should().Contain(item =>
            item.Contains("Copilot Extensions", StringComparison.OrdinalIgnoreCase));
        result.DoNotUseFor.Should().Contain(item =>
            item.Contains("Foundry agents", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_DoNotUseFor_ItemsDoNotLeakIntoUseFor()
    {
        var desc = "Build copilot apps. WHEN: copilot SDK, deploy copilot app. "
                 + "DO NOT USE FOR: general web apps without copilot SDK (use azure-prepare), "
                 + "Copilot Extensions, Foundry agents (use microsoft-foundry).";
        var content = WrapDescription("azure-hosted-copilot-sdk", desc);

        var result = _parser.Parse("azure-hosted-copilot-sdk", content);

        // WHEN items must be in UseFor
        result.UseFor.Should().Contain(item =>
            item.Contains("copilot SDK", StringComparison.OrdinalIgnoreCase));
        result.UseFor.Should().Contain(item =>
            item.Contains("deploy copilot app", StringComparison.OrdinalIgnoreCase));

        // DO NOT USE FOR items must NOT be in UseFor
        result.UseFor.Should().NotContain(item =>
            item.Contains("general web apps", StringComparison.OrdinalIgnoreCase));
        result.UseFor.Should().NotContain(item =>
            item.Contains("Copilot Extensions", StringComparison.OrdinalIgnoreCase));
        result.UseFor.Should().NotContain(item =>
            item.Contains("Foundry agents", StringComparison.OrdinalIgnoreCase));
    }

    // ==========================================================================
    // Test 2: DON'T USE WHEN regression test
    // ==========================================================================

    [Fact]
    public void Parse_DontUseWhen_StillRoutesToDoNotUseFor()
    {
        var desc = "Query and analyze data in Azure Data Explorer (Kusto/ADX). "
                 + "WHEN: KQL queries, Kusto database queries, Azure Data Explorer. "
                 + "DON'T USE WHEN: real-time streaming, database migrations.";
        var content = WrapDescription("azure-kusto", desc);

        var result = _parser.Parse("azure-kusto", content);

        result.DoNotUseFor.Should().Contain(item =>
            item.Contains("real-time streaming", StringComparison.OrdinalIgnoreCase));
        result.DoNotUseFor.Should().Contain(item =>
            item.Contains("database migrations", StringComparison.OrdinalIgnoreCase));

        // WHEN items still parse into UseFor
        result.UseFor.Should().Contain(item =>
            item.Contains("KQL queries", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_DontUseWhen_DoesNotRegress_WithNewDoNotUseForSupport()
    {
        // Ensure both delimiters can coexist across different skills
        var descWithDontUseWhen = "Monitor resources. WHEN: analyze logs, KQL. "
                                + "DON'T USE WHEN: deploying resources.";
        var descWithDoNotUseFor = "Monitor resources. WHEN: analyze logs, KQL. "
                                + "DO NOT USE FOR: deploying resources.";

        var result1 = _parser.Parse("azure-monitor",
            WrapDescription("azure-monitor", descWithDontUseWhen));
        var result2 = _parser.Parse("azure-monitor",
            WrapDescription("azure-monitor", descWithDoNotUseFor));

        // Both should route negative items to DoNotUseFor
        result1.DoNotUseFor.Should().Contain(item =>
            item.Contains("deploying resources", StringComparison.OrdinalIgnoreCase));
        result2.DoNotUseFor.Should().Contain(item =>
            item.Contains("deploying resources", StringComparison.OrdinalIgnoreCase));
    }

    // ==========================================================================
    // Test 3: Description with BOTH WHEN and DO NOT USE FOR
    // ==========================================================================

    [Fact]
    public void Parse_WhenAndDoNotUseFor_CorrectSplit()
    {
        var desc = "Some tool. WHEN: query data, analyze logs. "
                 + "DO NOT USE FOR: real-time streaming, IoT telemetry.";
        var content = WrapDescription("azure-compute", desc);

        var result = _parser.Parse("azure-compute", content);

        result.UseFor.Should().Contain(item =>
            item.Contains("query data", StringComparison.OrdinalIgnoreCase));
        result.UseFor.Should().Contain(item =>
            item.Contains("analyze logs", StringComparison.OrdinalIgnoreCase));

        result.DoNotUseFor.Should().Contain(item =>
            item.Contains("real-time streaming", StringComparison.OrdinalIgnoreCase));
        result.DoNotUseFor.Should().Contain(item =>
            item.Contains("IoT telemetry", StringComparison.OrdinalIgnoreCase));

        // Cross-contamination check
        result.UseFor.Should().NotContain(item =>
            item.Contains("real-time streaming", StringComparison.OrdinalIgnoreCase));
        result.DoNotUseFor.Should().NotContain(item =>
            item.Contains("query data", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_AllThreeMarkers_WhenUseForDoNotUseFor()
    {
        var desc = "Instrument webapps with Azure Application Insights. "
                 + "USE FOR: telemetry patterns, SDK setup. "
                 + "WHEN: how to instrument app, App Insights SDK. "
                 + "DO NOT USE FOR: non-Azure APM tools, custom logging frameworks.";
        var content = WrapDescription("appinsights-instrumentation", desc);

        var result = _parser.Parse("appinsights-instrumentation", content);

        // USE FOR and WHEN both merge into UseFor
        result.UseFor.Should().Contain(item =>
            item.Contains("telemetry patterns", StringComparison.OrdinalIgnoreCase));
        result.UseFor.Should().Contain(item =>
            item.Contains("App Insights SDK", StringComparison.OrdinalIgnoreCase));

        result.DoNotUseFor.Should().Contain(item =>
            item.Contains("non-Azure APM tools", StringComparison.OrdinalIgnoreCase));
        result.DoNotUseFor.Should().Contain(item =>
            item.Contains("custom logging frameworks", StringComparison.OrdinalIgnoreCase));
    }

    // ==========================================================================
    // Test 6: Varied service examples (diverse skill names)
    // ==========================================================================

    [Theory]
    [InlineData("azure-cost",
        "Unified Azure cost management. WHEN: Azure costs, cost breakdown. DO NOT USE FOR: deploying resources, provisioning infrastructure.",
        new[] { "Azure costs", "cost breakdown" },
        new[] { "deploying resources", "provisioning infrastructure" })]
    [InlineData("azure-kusto",
        "Query Azure Data Explorer. WHEN: KQL queries, log analytics. DO NOT USE FOR: real-time dashboards, Grafana setup.",
        new[] { "KQL queries", "log analytics" },
        new[] { "real-time dashboards", "Grafana setup" })]
    [InlineData("azure-compute",
        "Azure VM management. WHEN: VM sizing, VMSS autoscale. DO NOT USE FOR: container orchestration, serverless functions.",
        new[] { "VM sizing", "VMSS autoscale" },
        new[] { "container orchestration", "serverless functions" })]
    public void Parse_DoNotUseFor_VariedSkills_CorrectRouting(
        string skillName, string desc, string[] expectedUseFor, string[] expectedDoNotUseFor)
    {
        var content = WrapDescription(skillName, desc);
        var result = _parser.Parse(skillName, content);

        foreach (var expected in expectedUseFor)
        {
            result.UseFor.Should().Contain(item =>
                item.Contains(expected, StringComparison.OrdinalIgnoreCase),
                because: $"'{expected}' should be in UseFor for {skillName}");
        }

        foreach (var expected in expectedDoNotUseFor)
        {
            result.DoNotUseFor.Should().Contain(item =>
                item.Contains(expected, StringComparison.OrdinalIgnoreCase),
                because: $"'{expected}' should be in DoNotUseFor for {skillName}");
        }
    }

    // ==========================================================================
    // Edge cases
    // ==========================================================================

    [Fact]
    public void Parse_DoNotUseFor_WithParenthetical_PreservesContext()
    {
        // Parenthetical "(use azure-prepare)" should not break parsing
        var desc = "Deploy apps. WHEN: run azd up, execute deployment. "
                 + "DO NOT USE FOR: creating new apps (use azure-prepare), setting up infrastructure.";
        var content = WrapDescription("azure-deploy", desc);

        var result = _parser.Parse("azure-deploy", content);

        result.DoNotUseFor.Should().HaveCountGreaterOrEqualTo(2);
        result.DoNotUseFor.Should().Contain(item =>
            item.Contains("creating new apps", StringComparison.OrdinalIgnoreCase));
        result.DoNotUseFor.Should().Contain(item =>
            item.Contains("setting up infrastructure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_NoDoNotUseFor_ReturnsEmptyList()
    {
        var desc = "Simple skill. WHEN: do things, manage stuff.";
        var content = WrapDescription("azure-storage", desc);

        var result = _parser.Parse("azure-storage", content);

        result.DoNotUseFor.Should().BeEmpty();
        result.UseFor.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_DoNotUseFor_AtEndOfDescription_NoTrailingGarbage()
    {
        var desc = "Debug issues. WHEN: troubleshoot AKS, analyze logs. "
                 + "DO NOT USE FOR: cost optimization, billing queries.";
        var content = WrapDescription("azure-diagnostics", desc);

        var result = _parser.Parse("azure-diagnostics", content);

        // Items should be clean — no trailing periods or marker fragments
        foreach (var item in result.DoNotUseFor)
        {
            item.Should().NotEndWith(".", because: "trailing periods should be cleaned");
            item.Should().NotContainAny(["DO NOT USE", "USE FOR", "WHEN:"],
                because: "marker text should not leak into items");
        }
    }
}
