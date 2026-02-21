using Xunit;
using ExamplePromptGeneratorStandalone.Utilities;

namespace ExamplePromptGeneratorStandalone.Tests;

public class FrontmatterUtilityTests
{
    [Fact]
    public void GenerateExamplePromptsFrontmatter_IncludesVersion()
    {
        var result = FrontmatterUtility.GenerateExamplePromptsFrontmatter("2.0.0-beta.21");

        Assert.Contains("mcp-cli.version: 2.0.0-beta.21", result);
    }

    [Fact]
    public void GenerateExamplePromptsFrontmatter_IncludesMsTopic()
    {
        var result = FrontmatterUtility.GenerateExamplePromptsFrontmatter("1.0.0");

        Assert.Contains("ms.topic: include", result);
    }

    [Fact]
    public void GenerateExamplePromptsFrontmatter_StartsAndEndsWithFrontmatterDelimiters()
    {
        var result = FrontmatterUtility.GenerateExamplePromptsFrontmatter("1.0.0");

        Assert.StartsWith("---", result);
        // Second delimiter should appear after the first line
        var lines = result.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        Assert.Equal("---", lines[0]);
        Assert.Contains("---", lines.Skip(1).ToArray());
    }

    [Fact]
    public void GenerateExamplePromptsFrontmatter_HandlesNullVersion()
    {
        var result = FrontmatterUtility.GenerateExamplePromptsFrontmatter(null);

        Assert.Contains("mcp-cli.version: unknown", result);
    }

    [Fact]
    public void GenerateInputPromptFrontmatter_IncludesToolCommand()
    {
        var result = FrontmatterUtility.GenerateInputPromptFrontmatter(
            "storage account list", "1.0.0", "storage-account-list-input-prompt.md", "test prompt");

        Assert.Contains("azmcp storage account list", result);
    }

    [Fact]
    public void GenerateInputPromptFrontmatter_IncludesVersion()
    {
        var result = FrontmatterUtility.GenerateInputPromptFrontmatter(
            "keyvault secret list", "2.0.0", "keyvault-secret-list-input-prompt.md", "test");

        Assert.Contains("mcp-cli.version: 2.0.0", result);
    }

    [Fact]
    public void GenerateInputPromptFrontmatter_IncludesIncludeReference()
    {
        var fileName = "advisor-recommendation-list-input-prompt.md";
        var result = FrontmatterUtility.GenerateInputPromptFrontmatter(
            "advisor recommendation list", "1.0.0", fileName, "test");

        Assert.Contains(fileName, result);
    }

    [Fact]
    public void GenerateInputPromptFrontmatter_IncludesUserPromptContent()
    {
        var userPrompt = "Generate 5 example prompts for the advisor tool.\nInclude required parameters.";
        var result = FrontmatterUtility.GenerateInputPromptFrontmatter(
            "advisor recommendation list", "1.0.0", "test.md", userPrompt);

        Assert.Contains("Generate 5 example prompts", result);
        Assert.Contains("Include required parameters", result);
    }

    [Fact]
    public void GenerateInputPromptFrontmatter_HandlesNullVersion()
    {
        var result = FrontmatterUtility.GenerateInputPromptFrontmatter(
            "test command", null, "test.md", "prompt");

        Assert.Contains("mcp-cli.version: unknown", result);
    }

    [Fact]
    public void GenerateInputPromptFrontmatter_IndentsUserPrompt()
    {
        var userPrompt = "Line 1\nLine 2";
        var result = FrontmatterUtility.GenerateInputPromptFrontmatter(
            "test", "1.0.0", "test.md", userPrompt);

        // Each line should be indented with 2 spaces in the YAML block scalar
        Assert.Contains("  Line 1", result);
        Assert.Contains("  Line 2", result);
    }
}
