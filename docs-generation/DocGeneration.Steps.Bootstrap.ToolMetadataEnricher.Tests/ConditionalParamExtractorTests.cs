using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Services;
using Xunit;

namespace DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Tests;

public sealed class ConditionalParamExtractorTests
{
    private readonly ConditionalParamExtractor _extractor = new();

    [Fact]
    public void Extract_WithRequiresAtLeastOne_ReturnsGroup()
    {
        var result = _extractor.Extract("Requires at least one of --sku, --service, or --region.");

        var group = Assert.Single(result);
        Assert.Equal("requires_at_least_one", group.Type);
        Assert.Equal(["--sku", "--service", "--region"], group.Parameters);
        Assert.Equal("description_regex", group.Source);
    }

    [Fact]
    public void Extract_WithMultipleConditionalGroups_ReturnsAll()
    {
        var description = "Requires at least one of --sku or --service. Requires at least one of --region or --subscription.";

        var result = _extractor.Extract(description);

        Assert.Collection(
            result,
            first => Assert.Equal(["--sku", "--service"], first.Parameters),
            second => Assert.Equal(["--region", "--subscription"], second.Parameters));
    }

    [Fact]
    public void Extract_WithNoConditionals_ReturnsEmpty()
    {
        var result = _extractor.Extract("Lists storage accounts in a subscription.");

        Assert.Empty(result);
    }

    [Fact]
    public void Extract_WithNullDescription_ReturnsEmpty()
    {
        Assert.Empty(_extractor.Extract(null));
    }

    [Fact]
    public void Extract_WithEmptyDescription_ReturnsEmpty()
    {
        Assert.Empty(_extractor.Extract(string.Empty));
    }

    [Fact]
    public void Extract_WithParametersDeduplicated_ReturnsDistinct()
    {
        var result = _extractor.Extract("Requires at least one of --sku, --sku, or --service.");

        var group = Assert.Single(result);
        Assert.Equal(["--sku", "--service"], group.Parameters);
    }

    [Fact]
    public void Extract_WithNoParametersInClause_SkipsGroup()
    {
        var result = _extractor.Extract("Requires at least one filter.");

        Assert.Empty(result);
    }
}
