using FluentAssertions;
using SkillsGen.Core.Generation;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Generation;

public class ExamplePromptsFallbackTests
{
    [Fact]
    public void GenerateFallbackPrompts_EmptyList_ReturnsEmpty()
    {
        var result = SkillPageGenerator.GenerateFallbackPrompts([], "Azure Storage");
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateFallbackPrompts_VerbPhrase_ConvertsToHowDoI()
    {
        var result = SkillPageGenerator.GenerateFallbackPrompts(
            ["deploy copilot app", "create storage account"], "Test Skill");

        result.Should().Contain("How do I deploy copilot app?");
        result.Should().Contain("How do I create storage account?");
    }

    [Fact]
    public void GenerateFallbackPrompts_NounPhrase_ConvertsToWorkWith()
    {
        var result = SkillPageGenerator.GenerateFallbackPrompts(
            ["semantic caching", "token metrics"], "AI Gateway");

        result.Should().Contain("How do I work with semantic caching?");
        result.Should().Contain("How do I work with token metrics?");
    }

    [Fact]
    public void GenerateFallbackPrompts_QuestionPassthrough_KeptAsIs()
    {
        var result = SkillPageGenerator.GenerateFallbackPrompts(
            ["How do I configure SSL?"], "Test Skill");

        result.Should().Contain("How do I configure SSL?");
    }

    [Fact]
    public void GenerateFallbackPrompts_CapAtMaxPrompts()
    {
        var items = Enumerable.Range(1, 20)
            .Select(i => $"deploy service {i}")
            .ToList();

        var result = SkillPageGenerator.GenerateFallbackPrompts(items, "Test");

        // MaxExamplePrompts is 8 (§7.3)
        result.Count.Should().BeLessOrEqualTo(8);
    }

    [Fact]
    public void ConvertToPrompt_TrailingPeriodStripped()
    {
        var result = SkillPageGenerator.ConvertToPrompt("deploy to Azure.", "Test");
        result.Should().Be("How do I deploy to Azure?");
    }

    [Fact]
    public void ConvertToPrompt_MixedCaseVerbRecognized()
    {
        var result = SkillPageGenerator.ConvertToPrompt("Monitor resource health", "Test");
        result.Should().Be("How do I monitor resource health?");
    }

    [Fact]
    public void ConvertToPrompt_ExistingQuestion_Preserved()
    {
        var result = SkillPageGenerator.ConvertToPrompt("What is the cost?", "Test");
        result.Should().Be("What is the cost?");
    }

    // === Interrogative fragments framed as direct questions (not "How do I work with ...?") ===

    [Theory]
    [InlineData("is my code ready to deploy", "Is my code ready to deploy?")]
    [InlineData("can I ship this to Azure", "Can I ship this to Azure?")]
    [InlineData("does my app need a Dockerfile", "Does my app need a Dockerfile?")]
    [InlineData("what Azure services do I need", "What Azure services do I need?")]
    [InlineData("do I need a Dockerfile", "Do I need a Dockerfile?")]
    [InlineData("are my dependencies compatible", "Are my dependencies compatible?")]
    [InlineData("should I use a managed identity", "Should I use a managed identity?")]
    public void ConvertToPrompt_InterrogativePhrase_FramedAsDirectQuestion(string input, string expected)
    {
        var result = SkillPageGenerator.ConvertToPrompt(input, "Test");
        result.Should().Be(expected);
    }

    [Fact]
    public void ConvertToPrompt_InterrogativePhrase_NotWrappedInHowDoIWorkWith()
    {
        var result = SkillPageGenerator.ConvertToPrompt("is my code ready to deploy", "Test");
        result.Should().NotContain("How do I work with");
    }

    // === Common action verbs framed as "How do I ...?" (not "How do I work with ...?") ===

    [Theory]
    [InlineData("bring your app to Azure", "How do I bring your app to Azure?")]
    [InlineData("scan my repo for issues", "How do I scan my repo for issues?")]
    [InlineData("evaluate my project for readiness", "How do I evaluate my project for readiness?")]
    public void ConvertToPrompt_ActionVerb_FramedAsHowDoI(string input, string expected)
    {
        var result = SkillPageGenerator.ConvertToPrompt(input, "Test");
        result.Should().Be(expected);
    }
}
