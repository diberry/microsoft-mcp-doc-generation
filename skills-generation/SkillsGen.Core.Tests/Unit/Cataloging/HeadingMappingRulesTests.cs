using FluentAssertions;
using SkillsGen.Core.Cataloging;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Cataloging;

public class HeadingMappingRulesTests
{
    [Theory]
    [InlineData("Use cases", "When to use this skill")]
    [InlineData("Negative use cases", "When not to use this skill")]
    [InlineData("Azure services", "Azure services knowledge")]
    [InlineData("Prerequisites", "Prerequisites")]
    [InlineData("Required Inputs", "Prerequisites")]
    [InlineData("Rules", "Prerequisites")]
    [InlineData("RBAC", "Prerequisites (RBAC sub-section)")]
    [InlineData("Required Roles", "Prerequisites (RBAC sub-section)")]
    [InlineData("Role Based Access", "Prerequisites (RBAC sub-section)")]
    [InlineData("Related Skills", "Related skills")]
    [InlineData("Decision Guidance", "Decision guidance")]
    [InlineData("Decision", "Decision guidance")]
    [InlineData("Guidance", "Decision guidance")]
    public void GetMapping_KnownHeading_ReturnsCorrectDestination(string heading, string expected)
    {
        HeadingMappingRules.GetMapping(heading).Should().Be(expected);
    }

    [Theory]
    [InlineData("MCP tools")]
    [InlineData("Workflow steps")]
    [InlineData("Steps")]
    [InlineData("Workflows")]
    [InlineData("Workflow")]
    public void GetMapping_ExcludedHeading_ReturnsNull(string heading)
    {
        HeadingMappingRules.IsKnown(heading).Should().BeTrue();
        HeadingMappingRules.GetMapping(heading).Should().BeNull();
    }

    [Theory]
    [InlineData("New Section")]
    [InlineData("Custom Heading")]
    [InlineData("Overview")]
    public void GetMapping_UnknownHeading_ReturnsNull(string heading)
    {
        HeadingMappingRules.GetMapping(heading).Should().BeNull();
    }

    [Theory]
    [InlineData("use cases", "When to use this skill")]
    [InlineData("USE CASES", "When to use this skill")]
    [InlineData("Prerequisites", "Prerequisites")]
    [InlineData("PREREQUISITES", "Prerequisites")]
    [InlineData("workflow", null)]
    [InlineData("WORKFLOW", null)]
    public void GetMapping_CaseInsensitive_ReturnsCorrectResult(string heading, string? expected)
    {
        HeadingMappingRules.GetMapping(heading).Should().Be(expected);
    }

    [Theory]
    [InlineData("  Prerequisites  ")]
    [InlineData("\tWorkflow\t")]
    public void IsKnown_HeadingWithWhitespace_MatchesAfterTrim(string heading)
    {
        HeadingMappingRules.IsKnown(heading).Should().BeTrue();
    }

    [Fact]
    public void IsKnown_UnknownHeading_ReturnsFalse()
    {
        HeadingMappingRules.IsKnown("Totally Unknown Section").Should().BeFalse();
    }
}
