using FluentAssertions;
using SkillsGen.Core.Generation;
using SkillsGen.Core.Models;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Generation;

public class NoOpRewriterTests
{
    private readonly NoOpRewriter _rewriter = new();

    [Fact]
    public async Task RewriteIntroAsync_ReturnsInputUnchanged()
    {
        var input = "This is the raw description of the skill.";
        var result = await _rewriter.RewriteIntroAsync("azure-test", input);

        result.Should().Be(input);
    }

    [Fact]
    public async Task GenerateKnowledgeOverviewAsync_ReturnsInputUnchanged()
    {
        var input = "# Full body content\n\nWith sections.";
        var result = await _rewriter.GenerateKnowledgeOverviewAsync("azure-test", input);

        result.Should().Be(input);
    }

    [Fact]
    public async Task TranslateWorkflowStepsAsync_ReturnsInputUnchanged()
    {
        var steps = new List<string> { "Step 1: discover mcp entry point", "Step 2: run tool" };
        var tools = new List<McpToolEntry> { new("tool1", "cmd", "purpose") };

        var result = await _rewriter.TranslateWorkflowStepsAsync("azure-test", steps, tools);

        result.Should().BeSameAs(steps);
    }

    [Fact]
    public async Task TranslateWorkflowStepsAsync_EmptyList_ReturnsEmpty()
    {
        var steps = new List<string>();
        var result = await _rewriter.TranslateWorkflowStepsAsync("azure-test", steps, []);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SynthesizeWhatItProvidesAsync_ReturnsNonNullNonEmptyString()
    {
        var skillData = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = "Manage Azure Storage resources.",
            Services = [new ServiceEntry("Blob Storage", "Store unstructured data")],
            McpTools = [new McpToolEntry("storage_list", "storage account list", "List storage accounts")]
        };

        var result = await _rewriter.SynthesizeWhatItProvidesAsync("azure-storage", skillData);

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Azure Storage");
    }

    [Fact]
    public async Task SynthesizeWhatItProvidesAsync_MatchesMechanicalOutput()
    {
        var skillData = new SkillData
        {
            Name = "azure-keyvault",
            DisplayName = "Azure Key Vault",
            Description = "Manage secrets and certificates.",
            Services = [new ServiceEntry("Key Vault", "Manage secrets")],
            McpTools = [new McpToolEntry("kv_get", "keyvault secret get", "retrieve secrets from a key vault")]
        };

        var result = await _rewriter.SynthesizeWhatItProvidesAsync("azure-keyvault", skillData);
        var mechanical = SkillPageGenerator.BuildWhatItProvides(skillData);

        result.Should().Be(mechanical);
    }

    [Fact]
    public async Task SynthesizeWhenToUseSummaryAsync_MatchesMechanicalOutput()
    {
        var skillData = new SkillData
        {
            Name = "azure-keyvault",
            DisplayName = "Azure Key Vault",
            Description = "Manage secrets and certificates.",
            UseFor = ["store secrets", "manage certificates"],
            DoNotUseFor = ["large blobs"]
        };

        var result = await _rewriter.SynthesizeWhenToUseSummaryAsync("azure-keyvault", skillData);
        var mechanical = SkillPageGenerator.BuildWhenToUseSummary(skillData);

        result.Should().Be(mechanical);
    }
}
