using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests that the tool-family-cleanup system prompt contains the editorial
/// feedback rules from PR #8978: abbreviation scannability and brand capitalization.
/// These tests would FAIL if the rules were reverted.
/// </summary>
public class EditorialFeedbackPromptRuleTests
{
    private readonly string _promptContent;

    public EditorialFeedbackPromptRuleTests()
    {
        var projectRoot = DocGeneration.TestInfrastructure.ProjectRootFinder.FindSolutionRoot();
        var promptPath = Path.Combine(
            projectRoot,
            "mcp-tools",
            "DocGeneration.Steps.ToolFamilyCleanup",
            "prompts",
            "tool-family-cleanup-system-prompt.txt");

        _promptContent = File.ReadAllText(promptPath);
    }

    // ΓöÇΓöÇ Abbreviation Scannability ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

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
    public void Prompt_ContainsAfterFirstUseAbbreviationOnlyRule()
    {
        Assert.Contains("After first use, use ONLY the abbreviation", _promptContent);
    }

    [Fact]
    public void Prompt_ContainsCommonAzureAbbreviations()
    {
        Assert.Contains("VMSS", _promptContent);
        Assert.Contains("VNet", _promptContent);
        Assert.Contains("NSG", _promptContent);
        Assert.Contains("ACR", _promptContent);
        Assert.Contains("AKS", _promptContent);
    }

    // ΓöÇΓöÇ Brand Capitalization ΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇΓöÇ

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

    [Fact]
    public void Prompt_ContainsBrandCapitalizationExamples()
    {
        Assert.Contains("Azure Kubernetes Service", _promptContent);
        Assert.Contains("Azure Container Registry", _promptContent);
    }
}
