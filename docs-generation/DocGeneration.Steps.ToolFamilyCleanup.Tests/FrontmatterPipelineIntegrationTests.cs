// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Integration tests verifying that required frontmatter fields survive
/// the full pipeline: DeterministicFrontmatterGenerator → Assemble → FamilyFileStitcher.Stitch().
/// The stitcher's post-processing chain includes FrontmatterEnricher which must inject
/// ms.date, author, ms.author, ai-usage, content_well_notification, and ms.custom.
///
/// Fixes: #219 — ms.date missing in generated tool-family files.
/// Decision: AD-007 (TDD — write failing tests before implementing fix).
/// </summary>
public class FrontmatterPipelineIntegrationTests
{
    private const string TestBrandName = "Azure Resource Health";
    private const string TestCliVersion = "2.0.0-beta.31+ed24dd9783f26645fd2b7218b4d52221b446354f";
    private const string TestSeoDescription = "Use Azure MCP Server tools to manage resource health and availability of Azure resources with natural language prompts from your IDE.";

    // ── ms.date present after full pipeline ─────────────────────────

    [Fact]
    public void Stitch_DeterministicFrontmatter_ContainsMsDate()
    {
        // Arrange — build metadata via DeterministicFrontmatterGenerator (exactly as production does)
        var header = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, 2, TestCliVersion, TestSeoDescription);
        var intros = "The Azure MCP Server lets you check resource health.\n\nAzure Resource Health helps you diagnose issues.";
        var metadata = DeterministicFrontmatterGenerator.Assemble(header, intros);

        var familyContent = CreateFamilyContent("resourcehealth", metadata);
        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(familyContent);

        // Assert — ms.date must be present with MM/dd/yyyy format
        Assert.Matches(@"ms\.date: \d{2}/\d{2}/\d{4}", result);
    }

    [Fact]
    public void Stitch_DeterministicFrontmatter_MsDateHasCorrectFormat()
    {
        var header = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, 2, TestCliVersion, TestSeoDescription);
        var metadata = DeterministicFrontmatterGenerator.Assemble(header, "Intro paragraph.");

        var familyContent = CreateFamilyContent("resourcehealth", metadata);
        var stitcher = new FamilyFileStitcher();

        var result = stitcher.Stitch(familyContent);

        // Extract ms.date value and verify it matches today's date
        var match = Regex.Match(result, @"ms\.date: (\d{2}/\d{2}/\d{4})");
        Assert.True(match.Success, "ms.date field not found in stitched output");

        var dateStr = match.Groups[1].Value;
        Assert.True(DateTime.TryParse(dateStr, out var parsedDate), $"ms.date value '{dateStr}' is not a valid date");
        Assert.Equal(DateTime.UtcNow.Date, parsedDate.Date);
    }

    // ── author fields present after full pipeline ───────────────────

    [Fact]
    public void Stitch_DeterministicFrontmatter_ContainsAuthor()
    {
        var header = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, 2, TestCliVersion, TestSeoDescription);
        var metadata = DeterministicFrontmatterGenerator.Assemble(header, "Intro.");

        var familyContent = CreateFamilyContent("resourcehealth", metadata);
        var stitcher = new FamilyFileStitcher();

        var result = stitcher.Stitch(familyContent);

        Assert.Contains("author: diberry", result);
    }

    [Fact]
    public void Stitch_DeterministicFrontmatter_ContainsMsAuthor()
    {
        var header = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, 2, TestCliVersion, TestSeoDescription);
        var metadata = DeterministicFrontmatterGenerator.Assemble(header, "Intro.");

        var familyContent = CreateFamilyContent("resourcehealth", metadata);
        var stitcher = new FamilyFileStitcher();

        var result = stitcher.Stitch(familyContent);

        Assert.Contains("ms.author: diberry", result);
    }

    // ── AI-usage and content well notification ──────────────────────

    [Fact]
    public void Stitch_DeterministicFrontmatter_ContainsAiUsage()
    {
        var header = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, 2, TestCliVersion, TestSeoDescription);
        var metadata = DeterministicFrontmatterGenerator.Assemble(header, "Intro.");

        var familyContent = CreateFamilyContent("resourcehealth", metadata);
        var stitcher = new FamilyFileStitcher();

        var result = stitcher.Stitch(familyContent);

        Assert.Contains("ai-usage: ai-generated", result);
    }

    [Fact]
    public void Stitch_DeterministicFrontmatter_ContainsContentWellNotification()
    {
        var header = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, 2, TestCliVersion, TestSeoDescription);
        var metadata = DeterministicFrontmatterGenerator.Assemble(header, "Intro.");

        var familyContent = CreateFamilyContent("resourcehealth", metadata);
        var stitcher = new FamilyFileStitcher();

        var result = stitcher.Stitch(familyContent);

        Assert.Contains("content_well_notification:", result);
        Assert.Contains("AI-contribution", result);
    }

    // ── ms.custom for campaign tracking ─────────────────────────────

    [Fact]
    public void Stitch_DeterministicFrontmatter_ContainsMsCustom()
    {
        var header = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, 2, TestCliVersion, TestSeoDescription);
        var metadata = DeterministicFrontmatterGenerator.Assemble(header, "Intro.");

        var familyContent = CreateFamilyContent("resourcehealth", metadata);
        var stitcher = new FamilyFileStitcher();

        var result = stitcher.Stitch(familyContent);

        Assert.Contains("ms.custom: build-2025", result);
    }

    // ── All enriched fields are inside frontmatter delimiters ───────

    [Fact]
    public void Stitch_DeterministicFrontmatter_AllEnrichedFieldsInsideFrontmatter()
    {
        var header = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, 2, TestCliVersion, TestSeoDescription);
        var metadata = DeterministicFrontmatterGenerator.Assemble(header, "Intro paragraph.");

        var familyContent = CreateFamilyContent("resourcehealth", metadata);
        var stitcher = new FamilyFileStitcher();

        var result = stitcher.Stitch(familyContent);
        var normalized = result.Replace("\r\n", "\n");

        // Extract just the frontmatter block
        var fmStart = normalized.IndexOf("---");
        var fmEnd = normalized.IndexOf("\n---", fmStart + 3);
        Assert.True(fmEnd > fmStart, "Could not find closing frontmatter delimiter");

        var frontmatter = normalized.Substring(fmStart, fmEnd + 4 - fmStart);

        // All enriched fields must be inside the frontmatter
        Assert.Contains("ms.date:", frontmatter);
        Assert.Contains("author:", frontmatter);
        Assert.Contains("ms.author:", frontmatter);
        Assert.Contains("ai-usage:", frontmatter);
        Assert.Contains("ms.custom:", frontmatter);
        Assert.Contains("content_well_notification:", frontmatter);
    }

    // ── Generator itself includes ms.date (defense in depth) ──────

    [Fact]
    public void Generate_IncludesMsDate()
    {
        var header = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, 2, TestCliVersion, TestSeoDescription);

        // Generator must produce ms.date directly — not rely solely on FrontmatterEnricher
        Assert.Contains("ms.date:", header);
        Assert.Matches(@"ms\.date: \d{2}/\d{2}/\d{4}", header);
    }

    [Fact]
    public void Generate_MsDateMatchesToday()
    {
        var header = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, 2, TestCliVersion, TestSeoDescription);

        var expected = DateTime.UtcNow.ToString("MM/dd/yyyy");
        Assert.Contains($"ms.date: {expected}", header);
    }

    // ── Enricher works on generator output directly ─────────────────

    [Fact]
    public void Enrich_GeneratorOutput_InjectsMsDate()
    {
        var header = DeterministicFrontmatterGenerator.Generate(
            TestBrandName, 2, TestCliVersion, TestSeoDescription);
        var metadata = DeterministicFrontmatterGenerator.Assemble(header, "Intro.");

        // Feed generator output directly to enricher
        var enriched = FrontmatterEnricher.Enrich(metadata);

        Assert.Contains("ms.date:", enriched);
        Assert.Matches(@"ms\.date: \d{2}/\d{2}/\d{4}", enriched);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static FamilyContent CreateFamilyContent(string familyName, string metadata)
    {
        return new FamilyContent
        {
            FamilyName = familyName,
            Metadata = metadata,
            Tools =
            [
                new ToolContent
                {
                    ToolName = "Get availability status",
                    FileName = "azure-resourcehealth-availability-get.md",
                    FamilyName = familyName,
                    Content = "## Get availability status\n<!-- @mcpcli resourcehealth availability get -->\n\nGets the availability status of a resource.",
                    Command = "resourcehealth availability get",
                    Description = "Gets the availability status of a resource."
                },
                new ToolContent
                {
                    ToolName = "List health events",
                    FileName = "azure-resourcehealth-events-list.md",
                    FamilyName = familyName,
                    Content = "## List health events\n<!-- @mcpcli resourcehealth events list -->\n\nLists health events for a resource.",
                    Command = "resourcehealth events list",
                    Description = "Lists health events for a resource."
                }
            ],
            RelatedContent = "## Related content\n\n- [Azure Resource Health overview](/azure/resource-health/overview)"
        };
    }
}
