namespace DocGeneration.Tools.Fingerprint.Tests;

public class MarkdownAnalyzerTests
{
    private const string SampleArticle = """
        ---
        title: Azure MCP Server tools for Azure Advisor
        description: Use Azure MCP tools for advisor recommendations.
        ms.date: 03/27/2026
        ms.service: azure-mcp-server
        ms.topic: concept-article
        tool_count: 3
        author: diberry
        ms.author: diberry
        ---

        # Azure MCP Server tools for Azure Advisor

        Azure Advisor provides personalized recommendations.

        ## Get advisor recommendations

        This tool retrieves recommendations from Azure Advisor.

        ## List advisor configurations

        This tool lists advisor configurations for a subscription.

        ## Related content

        - [Azure Advisor documentation](/azure/advisor/)
        """;

    private const string NoFrontmatter = """
        # No Frontmatter Here

        ## Section One

        Some content.

        ## Section Two

        More content.
        """;

    [Fact]
    public void AnalyzeArticle_ExtractsCorrectSectionCount()
    {
        var result = MarkdownAnalyzer.AnalyzeArticle(SampleArticle, "advisor.md");
        // H1 + 3 H2s = 4 sections
        Assert.Equal(4, result.SectionCount);
    }

    [Fact]
    public void AnalyzeArticle_ExtractsH2Headings()
    {
        var result = MarkdownAnalyzer.AnalyzeArticle(SampleArticle, "advisor.md");
        Assert.Equal(3, result.H2Headings.Count);
        Assert.Contains("## Get advisor recommendations", result.H2Headings);
        Assert.Contains("## List advisor configurations", result.H2Headings);
        Assert.Contains("## Related content", result.H2Headings);
    }

    [Fact]
    public void AnalyzeArticle_ExtractsFrontmatterFields()
    {
        var result = MarkdownAnalyzer.AnalyzeArticle(SampleArticle, "advisor.md");
        Assert.Contains("title", result.FrontmatterFields);
        Assert.Contains("description", result.FrontmatterFields);
        Assert.Contains("ms.date", result.FrontmatterFields);
        Assert.Contains("ms.service", result.FrontmatterFields);
        Assert.Contains("tool_count", result.FrontmatterFields);
        Assert.Contains("author", result.FrontmatterFields);
    }

    [Fact]
    public void AnalyzeArticle_ExtractsToolCount()
    {
        var result = MarkdownAnalyzer.AnalyzeArticle(SampleArticle, "advisor.md");
        Assert.Equal(3, result.ToolCount);
    }

    [Fact]
    public void AnalyzeArticle_NoToolCount_ReturnsNull()
    {
        var result = MarkdownAnalyzer.AnalyzeArticle(NoFrontmatter, "test.md");
        Assert.Null(result.ToolCount);
    }

    [Fact]
    public void AnalyzeArticle_SetsFileName()
    {
        var result = MarkdownAnalyzer.AnalyzeArticle(SampleArticle, "advisor.md");
        Assert.Equal("advisor.md", result.FileName);
    }

    [Fact]
    public void AnalyzeArticle_CountsWords()
    {
        var result = MarkdownAnalyzer.AnalyzeArticle(SampleArticle, "advisor.md");
        Assert.True(result.WordCount > 10, $"Expected >10 words, got {result.WordCount}");
    }

    [Fact]
    public void AnalyzeArticle_CalculatesSize()
    {
        var result = MarkdownAnalyzer.AnalyzeArticle(SampleArticle, "advisor.md");
        Assert.True(result.SizeBytes > 0);
    }

    [Fact]
    public void AnalyzeArticle_NoFrontmatter_ReturnsEmptyFields()
    {
        var result = MarkdownAnalyzer.AnalyzeArticle(NoFrontmatter, "test.md");
        Assert.Empty(result.FrontmatterFields);
    }

    [Fact]
    public void AnalyzeQuality_DetectsFutureTenseViolations()
    {
        var content = """
            ---
            title: Test
            ---

            The tool will return a list of items. It will create a resource.
            """;
        var result = MarkdownAnalyzer.AnalyzeQuality(content);
        Assert.Equal(2, result.FutureTenseViolations);
    }

    [Fact]
    public void AnalyzeQuality_NoViolations_ReturnsZeros()
    {
        var content = """
            ---
            title: Test
            ---

            The tool returns a list of items. It creates a resource.
            """;
        var result = MarkdownAnalyzer.AnalyzeQuality(content);
        Assert.Equal(0, result.FutureTenseViolations);
        Assert.Equal(0, result.FabricatedUrlCount);
        Assert.Equal(0, result.BrandingViolations);
    }

    [Fact]
    public void AnalyzeQuality_DetectsFabricatedUrls()
    {
        var content = """
            ---
            title: Test
            ---

            See [docs](https://learn.microsoft.com/azure/advisor/docs/some-page).
            """;
        var result = MarkdownAnalyzer.AnalyzeQuality(content);
        Assert.Equal(1, result.FabricatedUrlCount);
    }

    [Fact]
    public void AnalyzeQuality_DetectsBrandingViolations()
    {
        var content = """
            ---
            title: Test
            ---

            Use CosmosDB and Azure Active Directory for Azure VMs with MSSQL.
            Also configure Azure AD and AAD settings for VMSS.
            """;
        var result = MarkdownAnalyzer.AnalyzeQuality(content);
        Assert.Equal(7, result.BrandingViolations);
    }

    [Fact]
    public void AnalyzeQuality_CalculatesContractionRate()
    {
        var content = """
            ---
            title: Test
            ---

            It doesn't matter. It does not work. You can't do this.
            """;
        var result = MarkdownAnalyzer.AnalyzeQuality(content);
        // 2 contractions (doesn't, can't) out of 3 total (doesn't, can't, does not)
        Assert.True(result.ContractionRate > 0.5, $"Expected >50% contraction rate, got {result.ContractionRate:P0}");
        Assert.True(result.ContractionRate < 1.0, $"Expected <100% contraction rate, got {result.ContractionRate:P0}");
    }

    [Fact]
    public void StripFrontmatter_RemovesFrontmatter()
    {
        var result = MarkdownAnalyzer.StripFrontmatter(SampleArticle);
        Assert.DoesNotContain("---", result);
        Assert.DoesNotContain("ms.date", result);
        Assert.Contains("# Azure MCP Server tools", result);
    }

    [Fact]
    public void StripFrontmatter_NoFrontmatter_ReturnsOriginal()
    {
        var result = MarkdownAnalyzer.StripFrontmatter(NoFrontmatter);
        Assert.Contains("# No Frontmatter Here", result);
    }

    [Fact]
    public void ExtractH2Headings_EmptyContent_ReturnsEmpty()
    {
        var result = MarkdownAnalyzer.ExtractH2Headings("");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractH2Headings_IgnoresH1AndH3()
    {
        var content = """
            # H1 heading
            ## H2 heading
            ### H3 heading
            """;
        var result = MarkdownAnalyzer.ExtractH2Headings(content);
        Assert.Single(result);
        Assert.Equal("## H2 heading", result[0]);
    }

    [Fact]
    public void AnalyzeArticle_EmptyString_ReturnsZeros()
    {
        var result = MarkdownAnalyzer.AnalyzeArticle("", "empty.md");
        Assert.Equal(0, result.WordCount);
        Assert.Equal(0, result.SectionCount);
        Assert.Equal(0, result.SizeBytes);
        Assert.Empty(result.H2Headings);
        Assert.Empty(result.FrontmatterFields);
        Assert.Null(result.ToolCount);
    }

    [Fact]
    public void AnalyzeQuality_ZeroContractionOpportunities_ReturnsZeroRate()
    {
        var content = """
            ---
            title: Test
            ---

            Azure Storage manages blobs and tables.
            """;
        var result = MarkdownAnalyzer.AnalyzeQuality(content);
        Assert.Equal(0.0, result.ContractionRate);
    }

    [Fact]
    public void AnalyzeQuality_DetectsAzureAdShortForm()
    {
        var content = """
            ---
            title: Test
            ---

            Configure Azure AD authentication. Use AAD tokens for access.
            """;
        var result = MarkdownAnalyzer.AnalyzeQuality(content);
        Assert.Equal(2, result.BrandingViolations);
    }
}
