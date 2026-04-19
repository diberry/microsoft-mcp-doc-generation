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

        // MaxExamplePrompts is 10
        result.Count.Should().BeLessOrEqualTo(10);
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
}
