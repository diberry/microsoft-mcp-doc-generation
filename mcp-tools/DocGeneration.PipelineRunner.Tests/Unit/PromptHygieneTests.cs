// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;

namespace DocGeneration.PipelineRunner.Tests.Unit;

/// <summary>
/// Cross-cutting tests that enforce prompt hygiene rules across all pipeline steps.
/// Prevents re-introduction of legacy duplicate prompts, ensures shared Acrolinx rules
/// are referenced by all AI steps, and validates Step 3 scope boundaries.
/// </summary>
public class PromptHygieneTests
{
    private readonly string _docsGenDir;

    public PromptHygieneTests()
    {
        _docsGenDir = DocGeneration.TestInfrastructure.ProjectRootFinder.FindMcpToolsRoot();
    }

    // ── P1: No legacy duplicate prompts ─────────────────────────────

    /// <summary>
    /// The top-level docs-generation/prompts/ directory must NOT contain prompt files
    /// that duplicate step-specific prompts. These were cleaned up and must not be
    /// re-introduced. The only allowed subdirectory is instruction-generation/.
    /// </summary>
    [Theory]
    [InlineData("system-prompt-example-prompt.txt")]
    [InlineData("user-prompt-example-prompt.txt")]
    [InlineData("system-prompt-example-prompt-validation.txt")]
    [InlineData("user-prompt-example-prompt-validation.txt")]
    [InlineData("tool-family-cleanup-system-prompt.txt")]
    [InlineData("tool-family-cleanup-user-prompt.txt")]
    public void LegacyPromptFile_MustNotExist(string filename)
    {
        var legacyPath = Path.Combine(_docsGenDir, "prompts", filename);
        Assert.False(
            File.Exists(legacyPath),
            $"Legacy duplicate prompt file must not exist: prompts/{filename}. " +
            "Step-specific prompts live in their respective project directories.");
    }

    // ── P2: All AI steps reference {{ACROLINX_RULES}} ──────────────

    /// <summary>
    /// Every AI-powered pipeline step (Steps 2, 3, 4, 6) must reference the shared
    /// {{ACROLINX_RULES}} token in its system prompt so that Acrolinx compliance rules
    /// are applied uniformly across all generated content.
    /// </summary>
    [Theory]
    [InlineData(
        "Step 2 (Example Prompts Generation)",
        "DocGeneration.Steps.ExamplePrompts.Generation",
        "prompts/system-prompt-example-prompt.txt")]
    [InlineData(
        "Step 3 (Tool Generation Improvements)",
        "DocGeneration.Steps.ToolGeneration.Improvements",
        "prompts/system-prompt.txt")]
    [InlineData(
        "Step 4 (Tool Family Cleanup)",
        "DocGeneration.Steps.ToolFamilyCleanup",
        "prompts/tool-family-cleanup-system-prompt.txt")]
    [InlineData(
        "Step 6 (Horizontal Articles)",
        "DocGeneration.Steps.HorizontalArticles",
        "prompts/horizontal-article-system-prompt.txt")]
    public void AiStep_SystemPrompt_ReferencesAcrolinxRules(
        string stepName, string projectDir, string promptRelPath)
    {
        var promptPath = Path.Combine(_docsGenDir, projectDir, promptRelPath);
        Assert.True(File.Exists(promptPath), $"System prompt not found for {stepName}: {promptPath}");

        var content = File.ReadAllText(promptPath);
        Assert.Contains(
            "{{ACROLINX_RULES}}",
            content,
            StringComparison.Ordinal);
    }

    // ── P2: Step 3 must NOT contain formatting rules that belong to Step 4 ──

    /// <summary>
    /// Step 3 (ToolGeneration.Improvements) focuses on content improvement — better
    /// descriptions, clearer explanations, technical accuracy. Formatting rules
    /// (backtick formatting, CLI switch conversion, LLM guidance removal) are Step 4's
    /// responsibility. This test catches scope creep if formatting rules are re-added.
    /// </summary>
    [Fact]
    public void Step3_SystemPrompt_DoesNotContain_CliSwitchConversionRules()
    {
        var content = ReadStep3SystemPrompt();

        // Step 3 must not contain CLI switch name conversion rules
        Assert.DoesNotContain("--switch-name", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--resource-group", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--vm-name", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Step3_SystemPrompt_DoesNotContain_LlmGuidanceRemovalRules()
    {
        var content = ReadStep3SystemPrompt();

        // Step 3 must not contain LLM guidance removal instructions
        Assert.DoesNotContain(
            "Use this tool when",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Do not use this tool",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "LLM-specific usage guidance",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Step3_SystemPrompt_DoesNotContain_BacktickFormattingRules()
    {
        var content = ReadStep3SystemPrompt();

        // Step 3 must not contain backtick formatting rules
        Assert.DoesNotContain(
            "Do NOT use backticks around parameter names in tables",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Backtick formatting consistency",
            content,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Convert quoted parameter values to backticks",
            content,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Step3_SystemPrompt_StillContains_ContentQualityRules()
    {
        var content = ReadStep3SystemPrompt();

        // Step 3 MUST still contain content quality guidance
        Assert.Contains("Technical Accuracy", content);
        Assert.Contains("Content Quality", content);
        Assert.Contains("Voice and Tone", content);
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private string ReadStep3SystemPrompt()
    {
        var path = Path.Combine(
            _docsGenDir,
            "DocGeneration.Steps.ToolGeneration.Improvements",
            "prompts",
            "system-prompt.txt");
        Assert.True(File.Exists(path), $"Step 3 system prompt not found: {path}");
        return File.ReadAllText(path);
    }
}
