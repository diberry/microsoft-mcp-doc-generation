using DocGeneration.PromptRegression.Tests.Infrastructure;

namespace DocGeneration.PromptRegression.Tests.Tests;

/// <summary>
/// Tests that verify the QualityMetrics analyzer produces accurate measurements.
/// These are the foundation — if metrics are wrong, all comparisons are meaningless.
/// </summary>
public class QualityMetricsTests
{
    [Fact]
    public void Analyze_ValidArticle_ExtractsSections()
    {
        var content = """
            ---
            title: Azure MCP Server tools for Storage
            ms.topic: concept-article
            ms.date: 03/27/2026
            ---

            # Azure MCP Server tools for Storage

            Use Azure MCP Server tools to manage storage.

            ## Prerequisites

            You need an Azure subscription.

            ## List storage accounts

            This tool lists all storage accounts.

            ## Best practices

            Use managed identities.

            ## Related content

            - [Azure Storage docs](/azure/storage/)
            """;

        var metrics = QualityMetrics.Analyze(content);

        Assert.Equal(5, metrics.SectionCount); // 1 H1 + 4 H2 headings
        Assert.True(metrics.HasValidFrontmatter);
        Assert.Contains("title", metrics.FrontmatterFields);
        Assert.Contains("ms.topic", metrics.FrontmatterFields);
        Assert.Contains("ms.date", metrics.FrontmatterFields);
        Assert.Empty(metrics.MissingSections);
    }

    [Fact]
    public void Analyze_MissingFrontmatter_DetectsAbsence()
    {
        var content = "# No frontmatter here\n\nJust plain content.";
        var metrics = QualityMetrics.Analyze(content);

        Assert.False(metrics.HasValidFrontmatter);
        Assert.Empty(metrics.FrontmatterFields);
    }

    [Fact]
    public void Analyze_MissingSections_ReportsCorrectly()
    {
        var content = """
            ---
            title: Test
            ---

            # Test article

            ## Prerequisites

            Content here.
            """;

        var metrics = QualityMetrics.Analyze(content);

        Assert.Contains("## Best practices", metrics.MissingSections);
        Assert.Contains("## Related content", metrics.MissingSections);
        Assert.DoesNotContain("## Prerequisites", metrics.MissingSections);
    }

    [Fact]
    public void Analyze_CountsContractions()
    {
        var content = """
            ---
            title: Test
            ---

            # Test

            You don't need to configure this. It doesn't require setup.
            The tool isn't destructive. You can't undo deletions.
            """;

        var metrics = QualityMetrics.Analyze(content);

        Assert.Equal(4, metrics.ContractionCount);
    }

    [Fact]
    public void Analyze_CountsContractionOpportunities()
    {
        var content = """
            ---
            title: Test
            ---

            # Test

            You do not need to configure this. It does not require setup.
            The tool is not destructive. You cannot undo deletions.
            """;

        var metrics = QualityMetrics.Analyze(content);

        Assert.True(metrics.ContractionOpportunities > 0);
        Assert.Equal(0, metrics.ContractionCount);
        Assert.Equal(0.0, metrics.ContractionRate);
    }

    [Fact]
    public void Analyze_DetectsFutureTenseViolations()
    {
        var content = """
            ---
            title: Test
            ---

            # Test

            This tool will return a list of resources.
            The command will create a new instance.
            """;

        var metrics = QualityMetrics.Analyze(content);

        Assert.Equal(2, metrics.FutureTenseViolations);
    }

    [Fact]
    public void Analyze_NoFutureTense_ReportsZero()
    {
        var content = """
            ---
            title: Test
            ---

            # Test

            This tool returns a list of resources.
            The command creates a new instance.
            """;

        var metrics = QualityMetrics.Analyze(content);

        Assert.Equal(0, metrics.FutureTenseViolations);
    }

    [Fact]
    public void Analyze_DetectsFabricatedUrls()
    {
        var content = """
            ---
            title: Test
            ---

            # Test

            See [docs](https://learn.microsoft.com/azure/storage/docs/overview).
            Also [this](https://learn.microsoft.com/azure/compute/docs/vms).
            """;

        var metrics = QualityMetrics.Analyze(content);

        Assert.Equal(2, metrics.FabricatedUrlCount);
    }

    [Fact]
    public void Analyze_DetectsBrandingViolations()
    {
        var content = """
            ---
            title: Test
            ---

            # Test

            Use CosmosDB to store data. Configure Azure Active Directory for auth.
            Deploy to Azure VMs using MSSQL databases.
            """;

        var metrics = QualityMetrics.Analyze(content);

        Assert.Equal(4, metrics.BrandingViolations);
    }

    [Fact]
    public void Analyze_CleanContent_ReportsZeroViolations()
    {
        var content = """
            ---
            title: Azure MCP Server tools for Azure Cosmos DB
            ms.topic: concept-article
            ms.date: 03/27/2026
            ---

            # Azure MCP Server tools for Azure Cosmos DB

            Use these tools to manage Azure Cosmos DB resources.

            ## Prerequisites

            You need an Azure subscription and a Microsoft Entra ID account.

            ## Best practices

            Use managed identities for authentication. Don't hardcode credentials.

            ## Related content

            - [Azure Cosmos DB documentation](/azure/cosmos-db/)
            """;

        var metrics = QualityMetrics.Analyze(content);

        Assert.Equal(0, metrics.FutureTenseViolations);
        Assert.Equal(0, metrics.FabricatedUrlCount);
        Assert.Equal(0, metrics.BrandingViolations);
        Assert.Empty(metrics.MissingSections);
        Assert.True(metrics.HasValidFrontmatter);
    }

    [Fact]
    public void Analyze_WordCount_IsReasonable()
    {
        var content = "one two three four five";
        var metrics = QualityMetrics.Analyze(content);

        Assert.Equal(5, metrics.WordCount);
    }

    [Fact]
    public void Analyze_EmptyString_ReturnsZeroMetrics()
    {
        var metrics = QualityMetrics.Analyze("");

        Assert.Equal(0, metrics.SectionCount);
        Assert.Equal(0, metrics.WordCount);
        Assert.Equal(0, metrics.CharCount);
        Assert.False(metrics.HasValidFrontmatter);
        Assert.Equal(0, metrics.ContractionCount);
        Assert.Equal(0, metrics.FutureTenseViolations);
        Assert.Equal(0, metrics.FabricatedUrlCount);
        Assert.Equal(0, metrics.BrandingViolations);
    }

    [Fact]
    public void ContractionRate_NoOpportunities_ReturnsZero()
    {
        var content = "Just plain text with no contraction opportunities at all.";
        var metrics = QualityMetrics.Analyze(content);

        Assert.Equal(0.0, metrics.ContractionRate);
    }
}
