using FluentAssertions;
using SkillsGen.Core.Generation;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Generation;

public class WorkflowTranslationTests
{
    [Fact]
    public void ParseWorkflowStepsResponse_ValidJsonArray_ReturnsParsedSteps()
    {
        var json = """["Step one rewritten", "Step two rewritten"]""";
        var fallback = new List<string> { "raw 1", "raw 2" };

        var result = AzureOpenAiRewriter.ParseWorkflowStepsResponse(json, fallback);

        result.Should().HaveCount(2);
        result[0].Should().Be("Step one rewritten");
        result[1].Should().Be("Step two rewritten");
    }

    [Fact]
    public void ParseWorkflowStepsResponse_JsonInCodeBlock_ExtractsAndParses()
    {
        var response = """
            ```json
            ["You can explore available tools", "You can configure resources"]
            ```
            """;
        var fallback = new List<string> { "raw 1", "raw 2" };

        var result = AzureOpenAiRewriter.ParseWorkflowStepsResponse(response, fallback);

        result.Should().HaveCount(2);
        result[0].Should().Be("You can explore available tools");
    }

    [Fact]
    public void ParseWorkflowStepsResponse_JsonWithPreamble_ExtractsArray()
    {
        var response = """Here is the translated JSON: ["Step 1 translated", "Step 2 translated"]""";
        var fallback = new List<string> { "raw 1", "raw 2" };

        var result = AzureOpenAiRewriter.ParseWorkflowStepsResponse(response, fallback);

        result.Should().HaveCount(2);
        result[0].Should().Be("Step 1 translated");
    }

    [Fact]
    public void ParseWorkflowStepsResponse_InvalidJson_ReturnsFallback()
    {
        var response = "This is not valid JSON at all";
        var fallback = new List<string> { "raw step 1", "raw step 2" };

        var result = AzureOpenAiRewriter.ParseWorkflowStepsResponse(response, fallback);

        result.Should().BeSameAs(fallback);
    }

    [Fact]
    public void ParseWorkflowStepsResponse_EmptyArray_ReturnsFallback()
    {
        var response = "[]";
        var fallback = new List<string> { "raw 1" };

        var result = AzureOpenAiRewriter.ParseWorkflowStepsResponse(response, fallback);

        result.Should().BeSameAs(fallback);
    }
}
