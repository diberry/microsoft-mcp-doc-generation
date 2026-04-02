using FluentAssertions;
using SkillsGen.Core.Generation;
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
}
