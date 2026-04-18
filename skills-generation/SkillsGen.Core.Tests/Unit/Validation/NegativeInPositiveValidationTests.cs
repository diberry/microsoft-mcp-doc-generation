using FluentAssertions;
using SkillsGen.Core.Models;
using SkillsGen.Core.Validation;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Validation;

/// <summary>
/// Tests for GitHub Issue #443: Detect negative/redirect items that leaked into
/// the "When to use" section of generated skill pages.
///
/// These tests verify NEW validation behavior. They will fail until the
/// corresponding validation rule is added to SkillPageValidator.
/// </summary>
public class NegativeInPositiveValidationTests
{
    private readonly SkillPageValidator _validator = new();

    private static SkillData CreateSkillData(string name = "azure-test") => new()
    {
        Name = name,
        DisplayName = name.Replace("-", " "),
        Description = $"Test skill for {name}."
    };

    private static string BuildContentWithWhenToUseItems(string skillTitle, params string[] items)
    {
        var bulletList = string.Join("\n", items.Select(i => $"- {i}"));
        return $"""
            ---
            title: Azure skill for {skillTitle}
            description: Test skill for {skillTitle}.
            ---

            # Azure skill for {skillTitle}

            ## Prerequisites

            - GitHub Copilot with Azure extension

            ### When to use this skill

            Use this skill when you need to:

            {bulletList}

            ## What it provides

            Knowledge about GitHub Copilot and Azure services for managing {skillTitle} resources effectively.
            """;
    }

    // ==========================================================================
    // Test 4: Negative-in-positive validation detection (NEW BEHAVIOR)
    // These tests verify that SkillPageValidator detects "DO NOT USE FOR" items
    // that incorrectly ended up in the "When to use" section.
    // ==========================================================================

    [Fact]
    public void Validate_WhenToUseContainsRedirectItem_ReturnsWarning()
    {
        // "(use azure-prepare)" is a redirect — it should NOT be in "When to use"
        var content = BuildContentWithWhenToUseItems("Azure Copilot SDK",
            "Build copilot-powered applications",
            "Deploy copilot apps to Azure",
            "general web apps without copilot SDK (use azure-prepare)");

        var result = _validator.Validate(content, 1, CreateSkillData("azure-hosted-copilot-sdk"),
            new TriggerData([], [], null));

        result.Warnings.Should().Contain(w =>
            w.Contains("NEGATIVE_IN_POSITIVE", StringComparison.OrdinalIgnoreCase),
            because: "a '(use X)' redirect pattern in 'When to use' indicates a misrouted DO NOT USE FOR item");
    }

    [Fact]
    public void Validate_WhenToUseContainsMultipleRedirects_WarnsForEach()
    {
        var content = BuildContentWithWhenToUseItems("Azure Deploy",
            "Run azd up and azd deploy",
            "general web apps (use azure-prepare)",
            "Foundry agents (use microsoft-foundry)");

        var result = _validator.Validate(content, 1, CreateSkillData("azure-deploy"),
            new TriggerData([], [], null));

        // Should detect both redirect items
        result.Warnings.Should().Contain(w =>
            w.Contains("NEGATIVE_IN_POSITIVE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WhenToUseContainsNotForPhrase_ReturnsWarning()
    {
        var content = BuildContentWithWhenToUseItems("Azure Compute",
            "VM sizing and recommendations",
            "Not for container orchestration",
            "VMSS autoscale configuration");

        var result = _validator.Validate(content, 1, CreateSkillData("azure-compute"),
            new TriggerData([], [], null));

        result.Warnings.Should().Contain(w =>
            w.Contains("NEGATIVE_IN_POSITIVE", StringComparison.OrdinalIgnoreCase),
            because: "'Not for' in a 'When to use' item indicates a misrouted negative item");
    }

    // ==========================================================================
    // Test 5: No false positives in validation
    // ==========================================================================

    [Fact]
    public void Validate_LegitimateWithoutInCapability_NoFalsePositive()
    {
        // "deploy without downtime" is a legitimate use case, not a negative
        var content = BuildContentWithWhenToUseItems("Azure Deploy",
            "Deploy without downtime using blue-green deployments",
            "Push updates to production",
            "Execute zero-downtime releases");

        var result = _validator.Validate(content, 1, CreateSkillData("azure-deploy"),
            new TriggerData([], [], null));

        result.Warnings.Should().NotContain(w =>
            w.Contains("NEGATIVE_IN_POSITIVE", StringComparison.OrdinalIgnoreCase),
            because: "'without' in context of a capability is legitimate, not a redirect");
    }

    [Fact]
    public void Validate_NormalPositiveItems_NoFalsePositive()
    {
        // Standard "When to use" items should not trigger any warnings
        var content = BuildContentWithWhenToUseItems("Azure Cost",
            "Query historical Azure costs",
            "Forecast future spending trends",
            "Optimize and reduce cloud waste");

        var result = _validator.Validate(content, 1, CreateSkillData("azure-cost"),
            new TriggerData([], [], null));

        result.Warnings.Should().NotContain(w =>
            w.Contains("NEGATIVE_IN_POSITIVE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ParenthesisWithoutUseKeyword_NoFalsePositive()
    {
        // Parenthetical that is NOT a redirect — "(v2.60.0+)" or "(AKS)"
        var content = BuildContentWithWhenToUseItems("Azure Kubernetes",
            "Create AKS clusters (Automatic or Standard SKU)",
            "Configure networking (Azure CNI Overlay)",
            "Manage node pools (system and user)");

        var result = _validator.Validate(content, 1, CreateSkillData("azure-kubernetes"),
            new TriggerData([], [], null));

        result.Warnings.Should().NotContain(w =>
            w.Contains("NEGATIVE_IN_POSITIVE", StringComparison.OrdinalIgnoreCase),
            because: "parenthetical content without 'use' keyword is not a redirect pattern");
    }

    [Fact]
    public void Validate_InsteadKeyword_ReturnsWarning()
    {
        var content = BuildContentWithWhenToUseItems("AppInsights",
            "Instrument webapps with Application Insights",
            "Copilot Extensions — use azure-ai instead");

        var result = _validator.Validate(content, 1, CreateSkillData("appinsights-instrumentation"),
            new TriggerData([], [], null));

        result.Warnings.Should().Contain(w =>
            w.Contains("NEGATIVE_IN_POSITIVE", StringComparison.OrdinalIgnoreCase),
            because: "'instead' redirect in 'When to use' indicates a misrouted negative item");
    }
}
