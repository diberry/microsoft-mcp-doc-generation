using FluentAssertions;
using SkillsGen.Core.Models;
using SkillsGen.Core.Validation;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Validation;

public class DriftDetectorTests
{
    private readonly DriftDetector _detector = new();

    private const string GeneratedPath = "/output/azure-storage.md";
    private const string PublishedUrl = "https://learn.microsoft.com/en-us/azure/developer/azure-skills/skills/azure-storage";

    private static string WrapWithFrontmatter(string body) => $"""
        ---
        title: Test
        description: Test
        ---

        {body}
        """;

    #region Missing / Extra Sections

    [Fact]
    public void DetectDrift_MissingSectionInGenerated_ReturnsErrorItem()
    {
        var generated = WrapWithFrontmatter("""
            ## Prerequisites

            - GitHub Copilot

            ## What it provides

            Things.
            """);

        var published = WrapWithFrontmatter("""
            ## Prerequisites

            - GitHub Copilot

            ## Related tools

            - Azure CLI

            ## What it provides

            Things.
            """);

        var report = _detector.DetectDrift("azure-storage", generated, published, GeneratedPath, PublishedUrl);

        report.Items.Should().Contain(i =>
            i.Section == "Related tools" &&
            i.Severity == DriftSeverity.Error &&
            i.Description.Contains("missing from generated"));
    }

    [Fact]
    public void DetectDrift_ExtraSectionInGenerated_ReturnsInfoItem()
    {
        var generated = WrapWithFrontmatter("""
            ## Prerequisites

            - GitHub Copilot

            ## Sub-skills

            - Sub A
            - Sub B
            """);

        var published = WrapWithFrontmatter("""
            ## Prerequisites

            - GitHub Copilot
            """);

        var report = _detector.DetectDrift("azure-storage", generated, published, GeneratedPath, PublishedUrl);

        report.Items.Should().Contain(i =>
            i.Section == "Sub-skills" &&
            i.Severity == DriftSeverity.Info &&
            i.Category == DriftCategory.ContentPrStale);
    }

    #endregion

    #region Content Differences

    [Fact]
    public void DetectDrift_WordCountDifference_TriggersWarning()
    {
        var generated = WrapWithFrontmatter("""
            ## What it provides

            Short.
            """);

        var published = WrapWithFrontmatter("""
            ## What it provides

            This section contains a much longer description with many words about what the skill provides to users, including detailed information about capabilities, features, integration points, and usage patterns that are documented extensively.
            """);

        var report = _detector.DetectDrift("azure-storage", generated, published, GeneratedPath, PublishedUrl);

        report.Items.Should().Contain(i =>
            i.Section == "What it provides" &&
            i.Severity == DriftSeverity.Warning &&
            i.Description.Contains("Word count"));
    }

    [Fact]
    public void DetectDrift_BulletCountDifference_Detected()
    {
        var generated = WrapWithFrontmatter("""
            ## When to use

            - Scenario A
            - Scenario B
            """);

        var published = WrapWithFrontmatter("""
            ## When to use

            - Scenario A
            - Scenario B
            - Scenario C
            - Scenario D
            - Scenario E
            """);

        var report = _detector.DetectDrift("azure-storage", generated, published, GeneratedPath, PublishedUrl);

        report.Items.Should().Contain(i =>
            i.Section == "When to use" &&
            i.Description.Contains("Bullet count"));
    }

    [Fact]
    public void DetectDrift_TablePresenceAbsence_Detected()
    {
        var generated = WrapWithFrontmatter("""
            ## MCP tools

            - tool-a: Does A
            - tool-b: Does B
            """);

        var published = WrapWithFrontmatter("""
            ## MCP tools

            | Tool | Purpose |
            |------|---------|
            | tool-a | Does A |
            | tool-b | Does B |
            """);

        var report = _detector.DetectDrift("azure-storage", generated, published, GeneratedPath, PublishedUrl);

        report.Items.Should().Contain(i =>
            i.Section == "MCP tools" &&
            i.Description.Contains("table") &&
            i.Category == DriftCategory.GenerationBug);
    }

    #endregion

    #region Empty Content

    [Fact]
    public void DetectDrift_EmptyGeneratedContent_ReturnsError()
    {
        var report = _detector.DetectDrift("azure-storage", "", "## Prerequisites\n\nContent.", GeneratedPath, PublishedUrl);

        report.Items.Should().ContainSingle(i =>
            i.Severity == DriftSeverity.Error &&
            i.Category == DriftCategory.GenerationBug &&
            i.Description.Contains("empty"));
    }

    [Fact]
    public void DetectDrift_EmptyPublishedContent_ReturnsInfoItems()
    {
        var generated = WrapWithFrontmatter("## Prerequisites\n\n- GitHub Copilot");

        var report = _detector.DetectDrift("azure-storage", generated, "", GeneratedPath, PublishedUrl);

        report.Items.Should().ContainSingle(i =>
            i.Severity == DriftSeverity.Info &&
            i.Description.Contains("not yet available"));
    }

    #endregion

    #region Section Name Normalization

    [Fact]
    public void DetectDrift_SectionNameNormalization_MatchesVariants()
    {
        var generated = WrapWithFrontmatter("""
            ## When to use

            Use this for storage tasks.
            """);

        var published = WrapWithFrontmatter("""
            ## When to use this skill

            Use this for storage tasks.
            """);

        var report = _detector.DetectDrift("azure-storage", generated, published, GeneratedPath, PublishedUrl);

        // Should NOT report these as missing/extra — they match after normalization
        report.Items.Should().NotContain(i =>
            i.Description.Contains("missing from generated") &&
            i.Section.Contains("When to use", StringComparison.OrdinalIgnoreCase));
        report.Items.Should().NotContain(i =>
            i.Description.Contains("not in published") &&
            i.Section.Contains("When to use", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Categorization

    [Fact]
    public void CategorizeSection_KnownTemplateSection_ReturnsGenerationBug()
    {
        DriftDetector.CategorizeSection("Prerequisites").Should().Be(DriftCategory.GenerationBug);
        DriftDetector.CategorizeSection("When to use this skill").Should().Be(DriftCategory.GenerationBug);
        DriftDetector.CategorizeSection("What it provides").Should().Be(DriftCategory.GenerationBug);
        DriftDetector.CategorizeSection("Example prompts").Should().Be(DriftCategory.GenerationBug);
        DriftDetector.CategorizeSection("Related content").Should().Be(DriftCategory.GenerationBug);
    }

    [Fact]
    public void CategorizeSection_UnknownSection_ReturnsSourceDataGap()
    {
        DriftDetector.CategorizeSection("Related tools").Should().Be(DriftCategory.SourceDataGap);
        DriftDetector.CategorizeSection("Decision guidance").Should().Be(DriftCategory.SourceDataGap);
        DriftDetector.CategorizeSection("Architecture overview").Should().Be(DriftCategory.SourceDataGap);
    }

    #endregion

    #region Fix Suggestions

    [Fact]
    public void SuggestFix_MissingKnownSection_SuggestsTemplateCheck()
    {
        var fix = DriftDetector.SuggestFix("Prerequisites", "missing-from-generated");

        fix.Should().Contain("template rendering");
        fix.Should().Contain("Prerequisites");
    }

    [Fact]
    public void SuggestFix_MissingUnknownSection_SuggestsSourceAddition()
    {
        var fix = DriftDetector.SuggestFix("Decision guidance", "missing-from-generated");

        fix.Should().Contain("SKILL.md");
        fix.Should().Contain("template support");
    }

    [Fact]
    public void DetectDrift_AllItemsHaveSuggestedFix()
    {
        var generated = WrapWithFrontmatter("""
            ## Prerequisites

            - GitHub Copilot
            """);

        var published = WrapWithFrontmatter("""
            ## Prerequisites

            - GitHub Copilot
            - Azure CLI
            - Azure subscription

            ## Related tools

            - Azure CLI
            """);

        var report = _detector.DetectDrift("azure-storage", generated, published, GeneratedPath, PublishedUrl);

        report.Items.Should().AllSatisfy(item =>
            item.SuggestedFix.Should().NotBeNullOrWhiteSpace());
    }

    #endregion

    #region Report Metadata

    [Fact]
    public void DetectDrift_ReportContainsCorrectMetadata()
    {
        var generated = WrapWithFrontmatter("## Prerequisites\n\n- Copilot");
        var published = WrapWithFrontmatter("## Prerequisites\n\n- Copilot");

        var report = _detector.DetectDrift("azure-storage", generated, published, GeneratedPath, PublishedUrl);

        report.SkillName.Should().Be("azure-storage");
        report.GeneratedPath.Should().Be(GeneratedPath);
        report.PublishedUrl.Should().Be(PublishedUrl);
        report.DetectedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion
}
