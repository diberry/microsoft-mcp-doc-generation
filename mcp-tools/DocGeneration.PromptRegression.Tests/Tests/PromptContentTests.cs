using DocGeneration.PromptRegression.Tests.Infrastructure;

namespace DocGeneration.PromptRegression.Tests.Tests;

/// <summary>
/// Tests that verify prompt files contain expected rules and instructions.
/// These catch accidental prompt deletions or rule removals.
/// </summary>
public class PromptContentTests
{
    private static readonly string DocsGenRoot = ProjectRootFinder.FindMcpToolsRoot();

    [Theory]
    [InlineData("DocGeneration.Steps.ExamplePrompts.Generation/prompts/system-prompt-example-prompt.txt")]
    [InlineData("DocGeneration.Steps.ExamplePrompts.Validation/prompts/system-prompt-example-prompt-validation.txt")]
    [InlineData("DocGeneration.Steps.ToolFamilyCleanup/prompts/tool-family-cleanup-system-prompt.txt")]
    [InlineData("DocGeneration.Steps.HorizontalArticles/prompts/horizontal-article-system-prompt.txt")]
    [InlineData("DocGeneration.Steps.ToolGeneration.Improvements/prompts/system-prompt.txt")]
    [InlineData("DocGeneration.Steps.Bootstrap.BrandMappings/prompts/system-prompt.txt")]
    public void SystemPrompt_Exists_AndIsNotEmpty(string relativePath)
    {
        var fullPath = Path.Combine(DocsGenRoot, relativePath);
        Assert.True(File.Exists(fullPath), $"System prompt missing: {relativePath}");

        var content = File.ReadAllText(fullPath);
        Assert.True(content.Length > 50, $"System prompt suspiciously short ({content.Length} chars): {relativePath}");
    }

    [Theory]
    [InlineData("DocGeneration.Steps.ExamplePrompts.Generation/prompts/user-prompt-example-prompt.txt")]
    [InlineData("DocGeneration.Steps.ExamplePrompts.Validation/prompts/user-prompt-example-prompt-validation.txt")]
    [InlineData("DocGeneration.Steps.ToolFamilyCleanup/prompts/tool-family-cleanup-user-prompt.txt")]
    [InlineData("DocGeneration.Steps.HorizontalArticles/prompts/horizontal-article-user-prompt.txt")]
    [InlineData("DocGeneration.Steps.ToolGeneration.Improvements/prompts/user-prompt-template.txt")]
    [InlineData("DocGeneration.Steps.Bootstrap.BrandMappings/prompts/user-prompt.txt")]
    public void UserPrompt_Exists_AndIsNotEmpty(string relativePath)
    {
        var fullPath = Path.Combine(DocsGenRoot, relativePath);
        Assert.True(File.Exists(fullPath), $"User prompt missing: {relativePath}");

        var content = File.ReadAllText(fullPath);
        Assert.True(content.Length > 20, $"User prompt suspiciously short ({content.Length} chars): {relativePath}");
    }

    [Fact]
    public void HorizontalArticleSystemPrompt_ContainsAcrolinxRules()
    {
        var content = ReadPrompt("DocGeneration.Steps.HorizontalArticles/prompts/horizontal-article-system-prompt.txt");

        // Key Acrolinx compliance rules that must be present
        Assert.Contains("present tense", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("active voice", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToolFamilyCleanupPrompt_ContainsStyleRules()
    {
        var content = ReadPrompt("DocGeneration.Steps.ToolFamilyCleanup/prompts/tool-family-cleanup-system-prompt.txt");

        Assert.Contains("tool", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("markdown", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExamplePromptSystemPrompt_RequiresVariedExamples()
    {
        var content = ReadPrompt("DocGeneration.Steps.ExamplePrompts.Generation/prompts/system-prompt-example-prompt.txt");

        Assert.Contains("different", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("parameter", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AllSystemPrompts_HaveNoLegacyDuplicates()
    {
        // After #295 cleanup, no legacy prompts should exist in mcp-tools/prompts/
        var legacyDir = Path.Combine(DocsGenRoot, "prompts");
        if (!Directory.Exists(legacyDir)) return; // Directory might be gone entirely

        var legacyPrompts = Directory.GetFiles(legacyDir, "system-prompt*.txt")
            .Concat(Directory.GetFiles(legacyDir, "user-prompt*.txt"))
            .Concat(Directory.GetFiles(legacyDir, "tool-family-cleanup*.txt"))
            .ToList();

        Assert.Empty(legacyPrompts);
    }

    private static string ReadPrompt(string relativePath)
    {
        var fullPath = Path.Combine(DocsGenRoot, relativePath);
        Assert.True(File.Exists(fullPath), $"Prompt file missing: {relativePath}");
        var content = File.ReadAllText(fullPath);
        var dataDir = Path.Combine(DocsGenRoot, "data");
        return Shared.PromptTokenResolver.Resolve(content, dataDir);
    }
}
