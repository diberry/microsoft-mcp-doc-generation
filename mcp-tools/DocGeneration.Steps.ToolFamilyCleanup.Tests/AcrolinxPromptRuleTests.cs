// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests that the tool-family-cleanup system prompt contains required
/// Acrolinx compliance rules for issues #143, #145, and #146.
/// These tests would FAIL if the prompt rules were reverted.
/// </summary>
public class AcrolinxPromptRuleTests
{
    private readonly string _promptContent;

    public AcrolinxPromptRuleTests()
    {
        var projectRoot = DocGeneration.TestInfrastructure.ProjectRootFinder.FindSolutionRoot();
        // Load the actual system prompt file
        var promptPath = Path.Combine(
            projectRoot,
            "mcp-tools",
            "DocGeneration.Steps.ToolFamilyCleanup",
            "prompts",
            "tool-family-cleanup-system-prompt.txt");

        var rawContent = File.ReadAllText(promptPath);
        var dataDir = Path.Combine(projectRoot, "mcp-tools", "data");
        _promptContent = Shared.PromptTokenResolver.Resolve(rawContent, dataDir);
    }

    // ── #143: Split complex overview sentence ───────────────────────

    [Fact]
    public void Prompt_ContainsOverviewSplitRule()
    {
        // The prompt must instruct AI to split the overview into two sentences
        Assert.Contains("two sentences", _promptContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prompt_ContainsOverviewPurposeThenCapabilities()
    {
        // The prompt must describe: first sentence = purpose, second = capabilities
        Assert.Contains("purpose", _promptContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("capabilities", _promptContent, StringComparison.OrdinalIgnoreCase);
    }

    // ── #145: Present tense and contractions ────────────────────────

    [Fact]
    public void Prompt_ContainsContractionRule()
    {
        // The prompt must instruct use of contractions
        Assert.Contains("contraction", _promptContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prompt_ContainsPresentTenseRule()
    {
        // The prompt must explicitly mention avoiding future tense "will be"
        Assert.Contains("will be", _promptContent, StringComparison.OrdinalIgnoreCase);
    }

    // ── #146: Commas after introductory phrases ─────────────────────

    [Fact]
    public void Prompt_ContainsIntroductoryPhraseCommaRule()
    {
        // The prompt must instruct comma usage after introductory phrases
        Assert.Contains("introductory phrase", _promptContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prompt_ContainsIntroductoryExamples()
    {
        // The prompt must give examples of introductory phrases
        Assert.Contains("For each", _promptContent);
    }

    // ── Helper ──────────────────────────────────────────────────────
}
