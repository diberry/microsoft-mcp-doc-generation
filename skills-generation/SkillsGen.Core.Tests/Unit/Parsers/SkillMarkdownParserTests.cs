using FluentAssertions;
using SkillsGen.Core.Models;
using SkillsGen.Core.Parsers;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Parsers;

public class SkillMarkdownParserTests
{
    private readonly SkillMarkdownParser _parser = new();

    private static string LoadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        return File.ReadAllText(path);
    }

    [Fact]
    public void Parse_WellFormedSkill_ExtractsAllSections()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.Name.Should().Be("azure-storage");
        result.DisplayName.Should().Be("Azure Storage");
        result.Description.Should().Contain("Azure Storage");
        result.Services.Should().NotBeEmpty();
        result.McpTools.Should().NotBeEmpty();
        result.WorkflowSteps.Should().NotBeEmpty();
        result.DecisionGuidance.Should().NotBeEmpty();
        result.Prerequisites.Should().NotBeEmpty();
        result.RawBody.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsDefaultWithSkillName()
    {
        var result = _parser.Parse("azure-test", "");

        result.Name.Should().Be("azure-test");
        result.DisplayName.Should().Be("azure-test");
        result.Description.Should().BeEmpty();
        result.Services.Should().BeEmpty();
        result.McpTools.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NullContent_ReturnsDefaultWithSkillName()
    {
        var result = _parser.Parse("azure-test", null!);

        result.Name.Should().Be("azure-test");
        result.Services.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NoFrontmatter_GracefulFallback()
    {
        var content = "# My Skill\n\nSome content without frontmatter.\n";
        var result = _parser.Parse("azure-fallback", content);

        result.Name.Should().Be("azure-fallback");
        result.DisplayName.Should().Be("Azure Fallback");
        result.RawBody.Should().Contain("My Skill");
    }

    [Fact]
    public void Parse_UseForAndDoNotUseFor_ExtractedFromDescription()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.UseFor.Should().NotBeEmpty("USE FOR items are in the description");
        result.DoNotUseFor.Should().NotBeEmpty("DO NOT USE FOR items are in the description");
    }

    [Fact]
    public void Parse_Services_ParsedIntoServiceEntryRecords()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.Services.Should().HaveCountGreaterOrEqualTo(3);
        result.Services[0].Name.Should().Be("Azure Blob Storage");
        result.Services[0].UseWhen.Should().Contain("unstructured");
    }

    [Fact]
    public void Parse_McpTools_ParsedCorrectly()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.McpTools.Should().HaveCountGreaterOrEqualTo(3);
        result.McpTools[0].ToolName.Should().Be("storage_account_list");
        result.McpTools[0].Command.Should().Contain("storage account list");
    }

    [Fact]
    public void Parse_WorkflowSteps_ExtractsNumberedList()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.WorkflowSteps.Should().HaveCountGreaterOrEqualTo(3);
        result.WorkflowSteps[0].Should().Contain("Authenticate");
    }

    [Fact]
    public void Parse_Prerequisites_ExtractsBulletItems()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.Prerequisites.Should().Contain(p => p.Contains("Azure subscription"));
    }

    [Fact]
    public void Parse_RelatedSkills_ExtractsCrossReferences()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.RelatedSkills.Should().Contain("azure-deploy");
        result.RelatedSkills.Should().Contain("azure-diagnostics");
    }

    [Fact]
    public void Parse_DecisionGuidance_ExtractsTopicsAndOptions()
    {
        var content = LoadFixture("sample-skill.md");
        var result = _parser.Parse("azure-storage", content);

        result.DecisionGuidance.Should().NotBeEmpty();
        var firstTopic = result.DecisionGuidance[0];
        firstTopic.Topic.Should().NotBeNullOrEmpty();
        firstTopic.Options.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_MinimalSkill_ReturnsPartialData()
    {
        var content = LoadFixture("minimal-skill.md");
        var result = _parser.Parse("azure-quotas", content);

        result.Name.Should().Be("azure-quotas");
        result.DisplayName.Should().Be("Azure Quotas");
        result.Services.Should().BeEmpty();
        result.McpTools.Should().BeEmpty();
        result.WorkflowSteps.Should().BeEmpty();
    }
}
