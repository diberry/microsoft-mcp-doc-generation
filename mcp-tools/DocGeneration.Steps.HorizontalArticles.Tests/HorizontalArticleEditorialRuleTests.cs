using Xunit;

namespace DocGeneration.Steps.HorizontalArticles.Tests;

/// <summary>
/// Tests that the horizontal-article system prompt contains editorial feedback rules
/// from PR #8978: abbreviation scannability and brand capitalization.
/// These tests would FAIL if the rules were reverted.
/// </summary>
public class HorizontalArticleEditorialRuleTests
{
    private readonly string _promptContent;

    public HorizontalArticleEditorialRuleTests()
    {
        var projectRoot = DocGeneration.TestInfrastructure.ProjectRootFinder.FindSolutionRoot();
        var promptPath = Path.Combine(
            projectRoot,
            "mcp-tools",
            "DocGeneration.Steps.HorizontalArticles",
            "prompts",
            "horizontal-article-system-prompt.txt");

        _promptContent = File.ReadAllText(promptPath);
    }

    [Fact]
    public void Prompt_ContainsAbbreviationScannabilitySection()
    {
        Assert.Contains("Abbreviation Scannability Rules", _promptContent);
    }

    [Fact]
    public void Prompt_ContainsFirstUseSpellOutRule()
    {
        Assert.Contains("spell out the full name with the abbreviation in parentheses", _promptContent);
    }

    [Fact]
    public void Prompt_ContainsBrandCapitalizationSection()
    {
        Assert.Contains("Brand Capitalization Rules", _promptContent);
    }

    [Fact]
    public void Prompt_ContainsNeverLowercaseRule()
    {
        Assert.Contains("Never lowercase service brand names", _promptContent);
    }
}
