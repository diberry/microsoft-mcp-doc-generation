using System.Collections.Generic;
using System.Linq;
using PipelineRunner.Validation;
using Xunit;

namespace PipelineRunner.Tests.Unit;

// Unit tests for the boilerplate-redundancy check (#662). Service-agnostic: fixtures draw
// prose from varied Azure services (Storage, Key Vault, Cosmos DB, Monitor, AKS, SQL) so
// the detection can never depend on any specific service name.
public class BoilerplateRedundancyTests
{
    [Fact]
    public void DetectBoilerplateRedundancyWarnings_IdenticalParagraphAcrossTwoSections_Warns()
    {
        const string boilerplate =
            "This tool operates on Azure resources and returns a structured response for the caller to process.";
        var input = new List<(string, IReadOnlyList<string>)>
        {
            ("List storage accounts", new[] { boilerplate }),
            ("Show storage account", new[] { boilerplate }),
        };

        var warnings = ToolFamilyPostAssemblyValidator.DetectBoilerplateRedundancyWarnings(input);

        var warning = Assert.Single(warnings);
        Assert.StartsWith("⚠️ Boilerplate redundancy", warning);
        Assert.Contains("2 tool sections", warning);
        Assert.Contains("List storage accounts", warning);
        Assert.Contains("Show storage account", warning);
    }

    [Fact]
    public void DetectBoilerplateRedundancyWarnings_DistinctParagraphs_NoWarning()
    {
        var input = new List<(string, IReadOnlyList<string>)>
        {
            ("Create Cosmos DB database", new[] { "Creates a new Azure Cosmos DB database inside the specified account for the workload." }),
            ("Query Log Analytics workspace", new[] { "Runs a Kusto query against the Azure Monitor Log Analytics workspace and returns rows." }),
        };

        var warnings = ToolFamilyPostAssemblyValidator.DetectBoilerplateRedundancyWarnings(input);

        Assert.Empty(warnings);
    }

    [Fact]
    public void DetectBoilerplateRedundancyWarnings_ShortSharedPhraseBelowThreshold_NoWarning()
    {
        // Fewer than the minimum word count: a short shared clause must not trip the check.
        var input = new List<(string, IReadOnlyList<string>)>
        {
            ("Get Key Vault secret", new[] { "Requires read access." }),
            ("List Key Vault secrets", new[] { "Requires read access." }),
        };

        var warnings = ToolFamilyPostAssemblyValidator.DetectBoilerplateRedundancyWarnings(input);

        Assert.Empty(warnings);
    }

    [Fact]
    public void DetectBoilerplateRedundancyWarnings_WhitespaceAndCaseDiffer_StillDetected()
    {
        var input = new List<(string, IReadOnlyList<string>)>
        {
            ("Scale AKS node pool", new[] { "Adjusts   the   node count for the managed cluster node pool to the requested value." }),
            ("Upgrade AKS node pool", new[] { "adjusts the node count FOR the managed cluster node pool to the requested value." }),
        };

        var warnings = ToolFamilyPostAssemblyValidator.DetectBoilerplateRedundancyWarnings(input);

        Assert.Single(warnings);
    }

    [Fact]
    public void DetectBoilerplateRedundancyWarnings_ParagraphInThreeSections_ListsAllThree()
    {
        const string boilerplate =
            "This operation targets a single Azure SQL database and validates the connection before running.";
        var input = new List<(string, IReadOnlyList<string>)>
        {
            ("Show SQL database", new[] { boilerplate }),
            ("List SQL databases", new[] { boilerplate }),
            ("Delete SQL database", new[] { boilerplate }),
        };

        var warnings = ToolFamilyPostAssemblyValidator.DetectBoilerplateRedundancyWarnings(input);

        var warning = Assert.Single(warnings);
        Assert.Contains("3 tool sections", warning);
        Assert.Contains("Show SQL database", warning);
        Assert.Contains("List SQL databases", warning);
        Assert.Contains("Delete SQL database", warning);
    }

    [Fact]
    public void DetectBoilerplateRedundancyWarnings_ParagraphRepeatedWithinOneSectionOnly_NoWarning()
    {
        const string boilerplate =
            "This tool operates on Azure Monitor metrics and returns aggregated values for the time range.";
        var input = new List<(string, IReadOnlyList<string>)>
        {
            ("Query Monitor metrics", new[] { boilerplate, boilerplate }),
            ("List Monitor metric definitions", new[] { "Lists the metric definitions available for the specified Azure Monitor resource scope." }),
        };

        var warnings = ToolFamilyPostAssemblyValidator.DetectBoilerplateRedundancyWarnings(input);

        Assert.Empty(warnings);
    }

    [Fact]
    public void ExtractDescriptionParagraphs_ReturnsProseBeforeStructuralElements()
    {
        var lines = new[]
        {
            "## Create storage container",
            "Creates a new blob container in the specified Azure Storage account with the given access level.",
            "<!-- @mcpcli storage container create -->",
            "Example prompts include:",
            "- Create a container named 'logs' in account 'stor1'",
            "| Parameter | Required |",
            "| --- | --- |",
            "| container name | Yes |",
        };

        var paragraphs = ToolFamilyPostAssemblyValidator.ExtractDescriptionParagraphs(lines);

        var paragraph = Assert.Single(paragraphs);
        Assert.Equal(
            "Creates a new blob container in the specified Azure Storage account with the given access level.",
            paragraph);
    }

    [Fact]
    public void ExtractDescriptionParagraphs_MultiLineParagraphJoinedWithSpace()
    {
        var lines = new[]
        {
            "## Query Cosmos DB container",
            "Runs a SQL query against the Azure Cosmos DB container",
            "and returns the matching items to the caller.",
            "<!-- @mcpcli cosmos container query -->",
        };

        var paragraphs = ToolFamilyPostAssemblyValidator.ExtractDescriptionParagraphs(lines);

        var paragraph = Assert.Single(paragraphs);
        Assert.Equal(
            "Runs a SQL query against the Azure Cosmos DB container and returns the matching items to the caller.",
            paragraph);
    }

    [Fact]
    public void ExtractDescriptionParagraphs_ExcludesTabMarkersAndIncludes()
    {
        var lines = new[]
        {
            "## Get Key Vault secret",
            "#### [Azure MCP CLI](#tab/azure-mcp-cli)",
            "Retrieves the value of a secret stored in the specified Azure Key Vault instance.",
            "[!INCLUDE [note](../includes/note.md)]",
            "> [!NOTE]",
            "> Requires the Key Vault Secrets User role.",
        };

        var paragraphs = ToolFamilyPostAssemblyValidator.ExtractDescriptionParagraphs(lines);

        var paragraph = Assert.Single(paragraphs);
        Assert.Equal(
            "Retrieves the value of a secret stored in the specified Azure Key Vault instance.",
            paragraph);
    }
}
