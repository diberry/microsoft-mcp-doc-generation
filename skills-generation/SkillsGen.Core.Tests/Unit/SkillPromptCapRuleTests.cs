using FluentAssertions;
using Xunit;

namespace SkillsGen.Core.Tests.Unit;

/// <summary>
/// Tests that the skill-page system prompt contains the maximum prompt cap rule.
/// This test would FAIL if the cap rule were reverted.
/// </summary>
public class SkillPromptCapRuleTests
{
    private readonly string _promptContent;

    public SkillPromptCapRuleTests()
    {
        // Navigate from test output to the skills-generation prompts directory
        var currentDir = AppContext.BaseDirectory;
        var repoRoot = currentDir;

        // Walk up until we find the solution root (has mcp-doc-generation.sln or skills-generation dir)
        while (repoRoot != null && !Directory.Exists(Path.Combine(repoRoot, "skills-generation")))
        {
            repoRoot = Directory.GetParent(repoRoot)?.FullName;
        }

        repoRoot.Should().NotBeNull("Could not find repo root with skills-generation directory");

        var promptPath = Path.Combine(
            repoRoot!,
            "skills-generation",
            "prompts",
            "skill-page-system-prompt.txt");

        File.Exists(promptPath).Should().BeTrue($"Prompt file should exist at {promptPath}");
        _promptContent = File.ReadAllText(promptPath);
    }

    [Fact]
    public void Prompt_ContainsMaximumEightPromptCapRule()
    {
        _promptContent.Should().Contain("Maximum 8 example prompts per skill");
    }

    [Fact]
    public void Prompt_ContainsDiversitySelectionGuidance()
    {
        _promptContent.Should().Contain("most diverse prompts");
    }
}
