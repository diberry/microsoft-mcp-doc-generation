using FluentAssertions;
using SkillsGen.Core.Parsers;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Parsers;

/// <summary>
/// Tests for GitHub Issue #441: Consolidate scattered prerequisites.
/// Verifies extraction from formal section, frontmatter compatibility, and inline body mentions.
/// </summary>
public class PrerequisiteExtractionTests
{
    private readonly SkillMarkdownParser _parser = new();

    private static string BuildSkill(string name, string frontmatter, string body) =>
        $"---\nname: {name}\n{frontmatter}---\n\n{body}\n";

    // ==========================================================================
    // Compatibility field extraction
    // ==========================================================================

    [Fact]
    public void Parse_CompatibilityField_ExtractsFromFrontmatter()
    {
        var content = BuildSkill("azure-aigateway",
            "description: \"Configure Azure API Management as an AI Gateway.\"\ncompatibility: Requires Azure CLI\n",
            "## Overview\n\nSome content.");

        var result = _parser.Parse("azure-aigateway", content);

        result.Compatibility.Should().ContainSingle()
            .Which.Should().Contain("Azure CLI");
    }

    [Fact]
    public void Parse_CompatibilityField_HandlesMultipleValues()
    {
        var content = BuildSkill("azure-deploy",
            "description: \"Deploy apps.\"\ncompatibility: Azure CLI, Docker, Node.js\n",
            "## Overview\n\nSome content.");

        var result = _parser.Parse("azure-deploy", content);

        result.Compatibility.Should().HaveCount(3);
        result.Compatibility.Should().Contain("Azure CLI");
        result.Compatibility.Should().Contain("Docker");
        result.Compatibility.Should().Contain("Node.js");
    }

    [Fact]
    public void Parse_NoCompatibilityField_ReturnsEmptyList()
    {
        var content = BuildSkill("azure-cost",
            "description: \"Manage costs.\"\n",
            "## Overview\n\nSome content.");

        var result = _parser.Parse("azure-cost", content);

        result.Compatibility.Should().BeEmpty();
    }

    // ==========================================================================
    // Inline prerequisite extraction
    // ==========================================================================

    [Fact]
    public void ExtractInlinePrerequisites_RequiresPattern_Detects()
    {
        var body = @"## Overview

This skill requires Azure CLI to function.

## Guidelines

The workspace must have Docker installed.";

        var result = SkillMarkdownParser.ExtractInlinePrerequisites(body);

        result.Should().Contain(item => item.Contains("Azure CLI", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExtractInlinePrerequisites_HostingPattern_Detects()
    {
        var body = @"## Guidelines

The app in the workspace must be one of these kinds:
- An ASP.NET Core app hosted in Azure
- A Node.js app hosted in Azure

## Other

More content.";

        var result = SkillMarkdownParser.ExtractInlinePrerequisites(body);

        result.Should().Contain(item => item.Contains("ASP.NET Core", StringComparison.OrdinalIgnoreCase));
        result.Should().Contain(item => item.Contains("Node.js", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExtractInlinePrerequisites_DockerRequired_Detects()
    {
        var body = @"## Rules

Docker must be installed and running for this skill to work.

## Usage

Some content.";

        var result = SkillMarkdownParser.ExtractInlinePrerequisites(body);

        result.Should().Contain("Docker");
    }

    [Fact]
    public void ExtractInlinePrerequisites_SkipsPrerequisitesSection()
    {
        var body = @"## Prerequisites

- Azure CLI
- Azure subscription

## Overview

This is the overview with no inline requirements.";

        var result = SkillMarkdownParser.ExtractInlinePrerequisites(body);

        // Should NOT re-extract "Azure CLI" from the formal section
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractInlinePrerequisites_EmptyBody_ReturnsEmpty()
    {
        var result = SkillMarkdownParser.ExtractInlinePrerequisites("");
        result.Should().BeEmpty();
    }

    // ==========================================================================
    // End-to-end: prerequisites merged from all sources
    // ==========================================================================

    [Fact]
    public void Parse_MergesPrerequisitesFromAllSources()
    {
        var content = BuildSkill("appinsights-instrumentation",
            "description: \"Instrument webapps with Azure Application Insights.\"\ncompatibility: Requires Azure CLI\n",
            @"## Prerequisites

- Azure subscription

## Guidelines

The app in the workspace must be one of these kinds:
- An ASP.NET Core app hosted in Azure

## Other

More content.");

        var result = _parser.Parse("appinsights-instrumentation", content);

        // Formal prerequisites
        result.Prerequisites.Should().Contain(item => item.Contains("Azure subscription", StringComparison.OrdinalIgnoreCase));
        // Inline prerequisites merged in
        result.Prerequisites.Should().Contain(item => item.Contains("ASP.NET Core", StringComparison.OrdinalIgnoreCase));
        // Compatibility tracked separately
        result.Compatibility.Should().Contain(item => item.Contains("Azure CLI", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_DeduplicatesPrerequisites()
    {
        var content = BuildSkill("azure-kubernetes",
            "description: \"AKS management.\"\n",
            @"## Prerequisites

- Azure CLI

## Overview

This skill requires Azure CLI for cluster management.");

        var result = _parser.Parse("azure-kubernetes", content);

        // Should not have duplicate Azure CLI entries
        var cliCount = result.Prerequisites.Count(p => p.Contains("Azure CLI", StringComparison.OrdinalIgnoreCase));
        cliCount.Should().BeLessOrEqualTo(1);
    }
}
