using FluentAssertions;
using SkillsGen.Core.Assessment;
using SkillsGen.Core.Models;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Assessment;

public class TierAssessorTests
{
    private readonly TierAssessor _assessor = new();

    private static SkillData CreateSkillData(
        int serviceCount = 0,
        int useForCount = 0,
        string description = "",
        int mcpToolCount = 0,
        int decisionCount = 0,
        int workflowStepCount = 0)
    {
        var services = Enumerable.Range(0, serviceCount)
            .Select(i => new ServiceEntry($"Azure Service {i}", $"Use when {i}", $"tool_{i}"))
            .ToList();

        var mcpTools = Enumerable.Range(0, mcpToolCount)
            .Select(i => new McpToolEntry($"tool_{i}", $"azure command {i}", $"Purpose {i}"))
            .ToList();

        var useFor = Enumerable.Range(0, useForCount)
            .Select(i => $"Use case {i}")
            .ToList();

        var decisions = Enumerable.Range(0, decisionCount)
            .Select(i => new DecisionEntry($"Topic {i}", [new DecisionOption($"Opt {i}", $"Best for {i}")]))
            .ToList();

        var workflowSteps = Enumerable.Range(0, workflowStepCount)
            .Select(i => $"Step {i}")
            .ToList();

        return new SkillData
        {
            Name = "test-skill",
            DisplayName = "Test Skill",
            Description = description,
            Services = services,
            McpTools = mcpTools,
            UseFor = useFor,
            DecisionGuidance = decisions,
            WorkflowSteps = workflowSteps
        };
    }

    private static TriggerData CreateTriggerData(int shouldTriggerCount = 0, int shouldNotCount = 0)
    {
        var should = Enumerable.Range(0, shouldTriggerCount)
            .Select(i => $"trigger prompt {i}")
            .ToList();
        var shouldNot = Enumerable.Range(0, shouldNotCount)
            .Select(i => $"not trigger {i}")
            .ToList();
        return new TriggerData(should, shouldNot, null);
    }

    [Fact]
    public void Assess_Q1_ThreeOrMoreServices_Scores2()
    {
        var skill = CreateSkillData(serviceCount: 3);
        var result = _assessor.Assess(skill, CreateTriggerData());

        result.Questions.First(q => q.Id == "Q1").Answer.Should().BeTrue();
    }

    [Fact]
    public void Assess_Q1_LessThanThreeServices_Scores0()
    {
        var skill = CreateSkillData(serviceCount: 2);
        var result = _assessor.Assess(skill, CreateTriggerData());

        result.Questions.First(q => q.Id == "Q1").Answer.Should().BeFalse();
    }

    [Fact]
    public void Assess_Q2_FiveOrMoreUseCases_Scores2()
    {
        var skill = CreateSkillData(useForCount: 5);
        var result = _assessor.Assess(skill, CreateTriggerData());

        result.Questions.First(q => q.Id == "Q2").Answer.Should().BeTrue();
    }

    [Fact]
    public void Assess_Q3_ThreeOrMoreTriggers_Scores1()
    {
        var skill = CreateSkillData();
        var triggers = CreateTriggerData(shouldTriggerCount: 3);
        var result = _assessor.Assess(skill, triggers);

        result.Questions.First(q => q.Id == "Q3").Answer.Should().BeTrue();
    }

    [Fact]
    public void Assess_Q4_DescriptionOver200Chars_Scores1()
    {
        var longDesc = new string('x', 200);
        var skill = CreateSkillData(description: longDesc);
        var result = _assessor.Assess(skill, CreateTriggerData());

        result.Questions.First(q => q.Id == "Q4").Answer.Should().BeTrue();
    }

    [Fact]
    public void Assess_Q4_ShortDescription_Scores0()
    {
        var skill = CreateSkillData(description: "Short");
        var result = _assessor.Assess(skill, CreateTriggerData());

        result.Questions.First(q => q.Id == "Q4").Answer.Should().BeFalse();
    }

    [Fact]
    public void Assess_Score3_ReturnsTier2()
    {
        // Q1=0(2svc) + Q2=2(5useFor) + Q3=1(3triggers) + Q4=0(short) + Q5=0(no azure ref) = 3
        var skill = CreateSkillData(serviceCount: 0, useForCount: 5);
        var triggers = CreateTriggerData(shouldTriggerCount: 3);
        var result = _assessor.Assess(skill, triggers);

        result.Tier.Should().Be(2);
    }

    [Fact]
    public void Assess_Score4_ReturnsTier1()
    {
        // Q1=2(3svc) + Q2=0(2useFor) + Q3=1(3triggers) + Q4=1(200+desc) + Q5=1(azure refs) = 5
        var skill = CreateSkillData(serviceCount: 3, description: new string('x', 200));
        var triggers = CreateTriggerData(shouldTriggerCount: 3);
        var result = _assessor.Assess(skill, triggers);

        result.Tier.Should().Be(1);
    }

    [Fact]
    public void Assess_FullTier1_AllFlagsEnabled()
    {
        var skill = CreateSkillData(
            serviceCount: 4, useForCount: 6,
            description: new string('x', 250),
            mcpToolCount: 3, decisionCount: 2, workflowStepCount: 4);
        var triggers = CreateTriggerData(shouldTriggerCount: 5);

        var result = _assessor.Assess(skill, triggers);

        result.Tier.Should().Be(1);
        result.ShowToolsSection.Should().BeTrue();
        result.ShowTriggersSection.Should().BeTrue();
        result.ShowDecisionGuidance.Should().BeTrue();
        result.ShowWorkflow.Should().BeTrue();
        result.ShowDetailedPrompts.Should().BeTrue();
    }

    [Fact]
    public void Assess_Tier2_ConditionalSectionsDisabled()
    {
        var skill = CreateSkillData(serviceCount: 1, useForCount: 1,
            description: "Short", mcpToolCount: 0, decisionCount: 1, workflowStepCount: 2);
        var triggers = CreateTriggerData(shouldTriggerCount: 1);

        var result = _assessor.Assess(skill, triggers);

        result.Tier.Should().Be(2);
        result.ShowDecisionGuidance.Should().BeFalse();
        result.ShowWorkflow.Should().BeFalse();
        result.ShowDetailedPrompts.Should().BeFalse();
    }

    [Fact]
    public void Assess_Q5_SpecificAzureServiceName_ReturnsTrue()
    {
        var skill = new SkillData
        {
            Name = "test-skill",
            DisplayName = "Test Skill",
            Description = "Test",
            Services = [new ServiceEntry("Azure Storage", "Store blobs")],
        };
        var result = _assessor.Assess(skill, CreateTriggerData());

        result.Questions.First(q => q.Id == "Q5").Answer.Should().BeTrue();
    }

    [Fact]
    public void Assess_Q5_GenericServiceName_ReturnsFalse()
    {
        var skill = new SkillData
        {
            Name = "test-skill",
            DisplayName = "Test Skill",
            Description = "Test",
            Services = [new ServiceEntry("My Custom Service", "Does stuff")],
        };
        var result = _assessor.Assess(skill, CreateTriggerData());

        result.Questions.First(q => q.Id == "Q5").Answer.Should().BeFalse();
    }

    [Fact]
    public void Assess_Q5_NoServicesOrTools_ReturnsFalse()
    {
        var skill = CreateSkillData(serviceCount: 0, mcpToolCount: 0);
        var result = _assessor.Assess(skill, CreateTriggerData());

        result.Questions.First(q => q.Id == "Q5").Answer.Should().BeFalse();
    }

    [Fact]
    public void Assess_Q5_McpToolWithAzureKeyword_ReturnsTrue()
    {
        var skill = new SkillData
        {
            Name = "test-skill",
            DisplayName = "Test Skill",
            Description = "Test",
            McpTools = [new McpToolEntry("cosmos-query", "Azure Cosmos DB query", "Query data")],
        };
        var result = _assessor.Assess(skill, CreateTriggerData());

        result.Questions.First(q => q.Id == "Q5").Answer.Should().BeTrue();
    }
}
