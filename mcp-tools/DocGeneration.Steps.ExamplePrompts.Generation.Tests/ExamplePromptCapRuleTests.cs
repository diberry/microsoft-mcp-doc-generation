using Xunit;

namespace DocGeneration.Steps.ExamplePrompts.Generation.Tests;

/// <summary>
/// Tests that the example prompt system prompt contains the maximum prompt cap rule.
/// These tests would FAIL if the cap rule were reverted.
/// </summary>
public class ExamplePromptCapRuleTests
{
    private readonly string _promptContent;

    public ExamplePromptCapRuleTests()
    {
        var currentDir = AppContext.BaseDirectory;
        var repoRoot = currentDir;

        while (repoRoot != null && !Directory.Exists(Path.Combine(repoRoot, "mcp-tools")))
        {
            repoRoot = Directory.GetParent(repoRoot)?.FullName;
        }

        Assert.NotNull(repoRoot);

        var promptPath = Path.Combine(
            repoRoot!,
            "mcp-tools",
            "DocGeneration.Steps.ExamplePrompts.Generation",
            "prompts",
            "system-prompt-example-prompt.txt");

        _promptContent = File.ReadAllText(promptPath);
    }

    [Fact]
    public void Prompt_ContainsMaximumPromptCapRule()
    {
        Assert.Contains("Never generate more than 10 example prompts per tool", _promptContent);
    }

    [Fact]
    public void Prompt_ContainsDiversitySelectionGuidance()
    {
        Assert.Contains("most diverse examples", _promptContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prompt_ContainsCapNumberTen()
    {
        Assert.Contains("maximum of 10", _promptContent, StringComparison.OrdinalIgnoreCase);
    }
}
