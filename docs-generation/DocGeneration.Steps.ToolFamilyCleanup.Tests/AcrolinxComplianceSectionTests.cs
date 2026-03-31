// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Verifies that ALL AI system prompts (Steps 2, 3, 4, 6) contain
/// dedicated Acrolinx compliance sections with required style guidelines.
/// These tests enforce the P0 Acrolinx improvement: adding style rules
/// directly into the prompts that generate documentation content.
/// </summary>
public class AcrolinxComplianceSectionTests
{
    private static readonly string ProjectRoot = DocGeneration.TestInfrastructure.ProjectRootFinder.FindSolutionRoot();

    /// <summary>
    /// All AI system prompt files that generate prose for published articles.
    /// Each must contain Acrolinx compliance instructions.
    /// </summary>
    public static IEnumerable<object[]> SystemPromptFiles =>
    [
        // Step 2 — Example prompt generation
        ["DocGeneration.Steps.ExamplePrompts.Generation", "prompts", "system-prompt-example-prompt.txt"],
        // Step 3 — Tool documentation improvements
        ["DocGeneration.Steps.ToolGeneration.Improvements", "prompts", "system-prompt.txt"],
        // Step 4 — Tool family article cleanup (primary article assembly)
        ["DocGeneration.Steps.ToolFamilyCleanup", "prompts", "tool-family-cleanup-system-prompt.txt"],
        // Step 6 — Horizontal (how-to) article generation
        ["DocGeneration.Steps.HorizontalArticles", "prompts", "horizontal-article-system-prompt.txt"],
    ];

    private static string LoadPrompt(string project, string folder, string file)
    {
        var path = Path.Combine(ProjectRoot, "docs-generation", project, folder, file);
        Assert.True(File.Exists(path), $"Prompt file not found: {path}");
        var content = File.ReadAllText(path);
        var dataDir = Path.Combine(ProjectRoot, "docs-generation", "data");
        return PromptTokenResolver.Resolve(content, dataDir);
    }

    // ── Present Tense ───────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(SystemPromptFiles))]
    public void Prompt_ContainsPresentTenseInstruction(string project, string folder, string file)
    {
        var content = LoadPrompt(project, folder, file);
        Assert.Contains("present tense", content, StringComparison.OrdinalIgnoreCase);
        // Must give a concrete example of what to avoid
        Assert.True(
            content.Contains("will return", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("will create", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("will list", StringComparison.OrdinalIgnoreCase),
            $"{file}: Must include a concrete 'will X' example to avoid");
    }

    // ── Contractions ────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(SystemPromptFiles))]
    public void Prompt_ContainsContractionInstruction(string project, string folder, string file)
    {
        var content = LoadPrompt(project, folder, file);
        Assert.Contains("contraction", content, StringComparison.OrdinalIgnoreCase);
        // Must show at least one concrete contraction example
        Assert.True(
            content.Contains("doesn't", StringComparison.Ordinal) ||
            content.Contains("don't", StringComparison.Ordinal) ||
            content.Contains("isn't", StringComparison.Ordinal),
            $"{file}: Must include concrete contraction examples (doesn't, don't, isn't)");
    }

    // ── Active Voice ────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(SystemPromptFiles))]
    public void Prompt_ContainsActiveVoiceInstruction(string project, string folder, string file)
    {
        var content = LoadPrompt(project, folder, file);
        Assert.Contains("active voice", content, StringComparison.OrdinalIgnoreCase);
    }

    // ── Introductory Commas ─────────────────────────────────────────

    [Theory]
    [MemberData(nameof(SystemPromptFiles))]
    public void Prompt_ContainsIntroductoryCommaInstruction(string project, string folder, string file)
    {
        var content = LoadPrompt(project, folder, file);
        // Must instruct adding commas after introductory phrases
        Assert.True(
            content.Contains("introductory phrase", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("introductory comma", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("comma after introductory", StringComparison.OrdinalIgnoreCase),
            $"{file}: Must contain introductory comma instructions");
        // Must include at least one example phrase
        Assert.True(
            content.Contains("For example,", StringComparison.Ordinal) ||
            content.Contains("By default,", StringComparison.Ordinal) ||
            content.Contains("In addition,", StringComparison.Ordinal),
            $"{file}: Must include example introductory phrases with commas");
    }

    // ── No First Person ─────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(SystemPromptFiles))]
    public void Prompt_ContainsNoFirstPersonInstruction(string project, string folder, string file)
    {
        var content = LoadPrompt(project, folder, file);
        // Must prohibit "we", "our", or "us" in generated content
        Assert.True(
            content.Contains("\"we\"", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("'we'", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("first person", StringComparison.OrdinalIgnoreCase) ||
            (content.Contains("Never use", StringComparison.OrdinalIgnoreCase) &&
             content.Contains("our", StringComparison.OrdinalIgnoreCase)),
            $"{file}: Must prohibit first person (we, our, us) in generated content");
    }

    // ── Acronym Expansion ───────────────────────────────────────────

    [Theory]
    [MemberData(nameof(SystemPromptFiles))]
    public void Prompt_ContainsAcronymExpansionInstruction(string project, string folder, string file)
    {
        var content = LoadPrompt(project, folder, file);
        Assert.True(
            content.Contains("acronym", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("first use", StringComparison.OrdinalIgnoreCase),
            $"{file}: Must contain acronym expansion instructions");
        // Must mention at least one concrete acronym example
        Assert.True(
            content.Contains("RBAC", StringComparison.Ordinal) ||
            content.Contains("MCP", StringComparison.Ordinal) ||
            content.Contains("AKS", StringComparison.Ordinal),
            $"{file}: Must include concrete acronym examples (RBAC, MCP, AKS)");
    }

    // ── Relative URLs ───────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(SystemPromptFiles))]
    public void Prompt_ContainsRelativeUrlInstruction(string project, string folder, string file)
    {
        var content = LoadPrompt(project, folder, file);
        // Must instruct site-root-relative URLs (/azure/...) instead of full URLs
        Assert.True(
            content.Contains("/azure/", StringComparison.Ordinal) &&
            (content.Contains("relative", StringComparison.OrdinalIgnoreCase) ||
             content.Contains("learn.microsoft.com", StringComparison.OrdinalIgnoreCase)),
            $"{file}: Must instruct use of relative URLs (/azure/...) not full learn.microsoft.com URLs");
    }

    // ── Sentence Length ─────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(SystemPromptFiles))]
    public void Prompt_ContainsSentenceLengthInstruction(string project, string folder, string file)
    {
        var content = LoadPrompt(project, folder, file);
        Assert.True(
            content.Contains("sentence", StringComparison.OrdinalIgnoreCase) &&
            (content.Contains("word", StringComparison.OrdinalIgnoreCase) ||
             content.Contains("split", StringComparison.OrdinalIgnoreCase) ||
             content.Contains("short", StringComparison.OrdinalIgnoreCase)),
            $"{file}: Must contain sentence length/splitting instructions");
    }

    // ── Wordy Phrases ───────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(SystemPromptFiles))]
    public void Prompt_ContainsWordyPhraseInstruction(string project, string folder, string file)
    {
        var content = LoadPrompt(project, folder, file);
        // Must instruct against at least one wordy phrase
        Assert.True(
            content.Contains("in order to", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("utilize", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("prior to", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("wordy", StringComparison.OrdinalIgnoreCase),
            $"{file}: Must instruct against wordy phrases (in order to, utilize, prior to)");
    }

    // ── Brand Compliance ────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(SystemPromptFiles))]
    public void Prompt_ContainsBrandComplianceInstruction(string project, string folder, string file)
    {
        var content = LoadPrompt(project, folder, file);
        // Must mention using official Azure service names
        Assert.True(
            content.Contains("Azure Cosmos DB", StringComparison.Ordinal) ||
            content.Contains("official", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("brand", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("service name", StringComparison.OrdinalIgnoreCase),
            $"{file}: Must contain brand compliance instructions for Azure service names");
    }

    // ── Shared Prompt Copies Also Updated ───────────────────────────

    [Fact]
    public void SharedPrompt_Step3_ContainsAcrolinxSection()
    {
        var content = File.ReadAllText(Path.Combine(
            ProjectRoot, "docs-generation", "DocGeneration.Steps.ToolGeneration.Improvements", "prompts", "system-prompt.txt"));
        var dataDir = Path.Combine(ProjectRoot, "docs-generation", "data");
        content = Shared.PromptTokenResolver.Resolve(content, dataDir);
        Assert.Contains("present tense", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("contraction", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("active voice", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SharedPrompt_Step4_ContainsAcrolinxSection()
    {
        var content = File.ReadAllText(Path.Combine(
            ProjectRoot, "docs-generation", "DocGeneration.Steps.ToolFamilyCleanup", "prompts", "tool-family-cleanup-system-prompt.txt"));
        var dataDir = Path.Combine(ProjectRoot, "docs-generation", "data");
        content = Shared.PromptTokenResolver.Resolve(content, dataDir);
        Assert.Contains("present tense", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("contraction", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("active voice", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sentence", content, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helper ──────────────────────────────────────────────────────
}
