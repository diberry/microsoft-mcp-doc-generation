// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Xunit;

namespace Shared.Tests;

/// <summary>
/// Tests for the centralized FrontmatterUtility in Shared.
/// Covers the core Generate method and all convenience methods.
/// </summary>
public class FrontmatterUtilityTests
{
    // ── Core Generate method ───────────────────────────────────

    [Fact]
    public void Generate_MinimalParams_ReturnsValidFrontmatter()
    {
        var result = FrontmatterUtility.Generate("include", "1.0.0");

        Assert.StartsWith("---", result);
        Assert.Contains("ms.topic: include", result);
        Assert.Contains("mcp-cli.version: 1.0.0", result);
        Assert.Contains("ms.date:", result);
    }

    [Fact]
    public void Generate_NullVersion_ShowsUnknown()
    {
        var result = FrontmatterUtility.Generate("include", null);

        Assert.Contains("mcp-cli.version: unknown", result);
    }

    [Fact]
    public void Generate_WithGeneratedDate_IncludesField()
    {
        var result = FrontmatterUtility.Generate("reference", "2.0.0",
            generatedDate: "2026-01-15 10:30:00 UTC");

        Assert.Contains("generated: 2026-01-15 10:30:00 UTC", result);
    }

    [Fact]
    public void Generate_WithoutGeneratedDate_OmitsField()
    {
        var result = FrontmatterUtility.Generate("include", "1.0.0");

        Assert.DoesNotContain("generated:", result);
    }

    [Fact]
    public void Generate_WithYamlComments_IncludesComments()
    {
        var result = FrontmatterUtility.Generate("include", "1.0.0",
            yamlComments: new[] { "# comment one", "# comment two" });

        Assert.Contains("# comment one", result);
        Assert.Contains("# comment two", result);
    }

    [Fact]
    public void Generate_WithExtraFields_IncludesFields()
    {
        var result = FrontmatterUtility.Generate("include", "1.0.0",
            extraFields: new[] { new KeyValuePair<string, string>("custom", "value") });

        Assert.Contains("custom: value", result);
    }

    [Fact]
    public void Generate_MsDateFallsBackToGeneratedDate()
    {
        var result = FrontmatterUtility.Generate("reference", "1.0.0",
            generatedDate: "2026-03-01 12:00:00 UTC");

        Assert.Contains("ms.date: 2026-03-01 12:00:00 UTC", result);
    }

    [Fact]
    public void Generate_ExplicitMsDate_OverridesGeneratedDate()
    {
        var result = FrontmatterUtility.Generate("include", "1.0.0",
            generatedDate: "2026-03-01 12:00:00 UTC",
            msDate: "2026-03-01");

        Assert.Contains("ms.date: 2026-03-01", result);
        Assert.DoesNotContain("ms.date: 2026-03-01 12:00:00", result);
    }

    // ── Annotation Frontmatter ─────────────────────────────────

    [Fact]
    public void GenerateAnnotationFrontmatter_ContainsExpectedFields()
    {
        var result = FrontmatterUtility.GenerateAnnotationFrontmatter(
            "storage account list", "2.5.0", "storage-account-list-annotations.md");

        Assert.Contains("ms.topic: include", result);
        Assert.Contains("mcp-cli.version: 2.5.0", result);
        Assert.Contains("generated:", result);
        Assert.Contains("# [!INCLUDE [storage account list](../includes/tools/annotations/storage-account-list-annotations.md)]", result);
        Assert.Contains("# azmcp storage account list", result);
    }

    [Fact]
    public void GenerateAnnotationFrontmatter_MsDateIsDateOnly()
    {
        var result = FrontmatterUtility.GenerateAnnotationFrontmatter(
            "test", "1.0.0", "test.md");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        Assert.Contains($"ms.date: {today}", result);
    }

    // ── Parameter Frontmatter ──────────────────────────────────

    [Fact]
    public void GenerateParameterFrontmatter_ContainsParametersPath()
    {
        var result = FrontmatterUtility.GenerateParameterFrontmatter(
            "keyvault secret get", "1.0.0", "keyvault-secret-get-parameters.md");

        Assert.Contains("../includes/tools/parameters/keyvault-secret-get-parameters.md", result);
        Assert.Contains("# azmcp keyvault secret get", result);
    }

    [Fact]
    public void GenerateParameterFrontmatter_DoesNotContainAnnotationsPath()
    {
        var result = FrontmatterUtility.GenerateParameterFrontmatter(
            "test", "1.0.0", "test.md");

        Assert.DoesNotContain("/annotations/", result);
    }

    // ── Example Prompts Frontmatter ────────────────────────────

    [Fact]
    public void GenerateExamplePromptsFrontmatter_NoGeneratedField()
    {
        var result = FrontmatterUtility.GenerateExamplePromptsFrontmatter("1.0.0");

        Assert.DoesNotContain("generated:", result);
    }

    [Fact]
    public void GenerateExamplePromptsFrontmatter_MsDateIncludesTime()
    {
        var result = FrontmatterUtility.GenerateExamplePromptsFrontmatter("1.0.0");

        // ms.date should have full timestamp format
        Assert.Contains("UTC", result);
    }

    // ── Input Prompt Frontmatter ───────────────────────────────

    [Fact]
    public void GenerateInputPromptFrontmatter_ContainsUserPrompt()
    {
        var result = FrontmatterUtility.GenerateInputPromptFrontmatter(
            "cosmos db list", "1.0.0", "cosmos-db-list-input-prompt.md",
            "Generate prompts for Cosmos DB.");

        Assert.Contains("userPrompt:", result);
        Assert.Contains("Generate prompts for Cosmos DB.", result);
    }

    [Fact]
    public void GenerateInputPromptFrontmatter_IndentsMultilinePrompt()
    {
        var result = FrontmatterUtility.GenerateInputPromptFrontmatter(
            "monitor log query", "1.0.0", "test.md", "Line 1\nLine 2");

        Assert.Contains("  Line 1", result);
        Assert.Contains("  Line 2", result);
    }

    // ── Raw Tool Frontmatter ───────────────────────────────────

    [Fact]
    public void GenerateRawToolFrontmatter_UsesReferenceTopicType()
    {
        var result = FrontmatterUtility.GenerateRawToolFrontmatter(
            "2.0.0-beta.13", "2026-01-24 00:06:16 UTC");

        Assert.Contains("ms.topic: reference", result);
        Assert.Contains("mcp-cli.version: 2.0.0-beta.13", result);
        Assert.Contains("generated: 2026-01-24 00:06:16 UTC", result);
        Assert.Contains("ms.date: 2026-01-24 00:06:16 UTC", result);
    }

    [Fact]
    public void GenerateRawToolFrontmatter_HandlesNullVersion()
    {
        var result = FrontmatterUtility.GenerateRawToolFrontmatter(null, "2026-01-24");

        Assert.Contains("mcp-cli.version: unknown", result);
    }

    // ── Structural ─────────────────────────────────────────────

    [Fact]
    public void AllMethods_StartAndEndWithFrontmatterDelimiters()
    {
        var methods = new[]
        {
            FrontmatterUtility.GenerateAnnotationFrontmatter("test", "1.0.0", "test.md"),
            FrontmatterUtility.GenerateParameterFrontmatter("test", "1.0.0", "test.md"),
            FrontmatterUtility.GenerateExamplePromptsFrontmatter("1.0.0"),
            FrontmatterUtility.GenerateInputPromptFrontmatter("test", "1.0.0", "test.md", "prompt"),
            FrontmatterUtility.GenerateRawToolFrontmatter("1.0.0", "2026-01-01"),
        };

        foreach (var result in methods)
        {
            Assert.StartsWith("---", result);
            var lines = result.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
            Assert.Equal("---", lines[0]);
            // Find closing --- marker
            var closingIdx = Array.FindIndex(lines, 1, l => l == "---");
            Assert.True(closingIdx > 0, $"Missing closing --- in:\n{result}");
        }
    }
}
