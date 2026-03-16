using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Models;
using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Services;
using Xunit;

namespace DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Tests;

public sealed class ConditionalParamExtractorTests
{
    private readonly ConditionalParamExtractor _extractor = new();

    // --- requires_at_least_one ---

    [Fact]
    public void Extract_WithRequiresAtLeastOne_ReturnsGroup()
    {
        var result = _extractor.Extract("Requires at least one of --sku, --service, or --region.");

        var group = result.First(g => g.Type == "requires_at_least_one");
        Assert.Equal(["--sku", "--service", "--region"], group.Parameters);
        Assert.Equal("description_regex", group.Source);
        Assert.NotNull(group.Description);
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

        var group = result.First(g => g.Type == "requires_at_least_one");
        Assert.Equal(["--sku", "--service"], group.Parameters);
    }

    [Fact]
    public void Extract_WithNoParametersInClause_SkipsRequiresAtLeastOne()
    {
        var result = _extractor.Extract("Requires at least one filter.");

        Assert.DoesNotContain(result, g => g.Type == "requires_at_least_one");
    }

    // --- list_or_get ---

    [Fact]
    public void Extract_ListOrGet_WithIfProvided_ReturnsSwitchParam()
    {
        var description = "Get details of a specific file share or list all file shares. If --name is provided, returns a specific file share.";

        var group = result(description, "list_or_get");
        Assert.Equal(["--name"], group.Parameters);
    }

    [Fact]
    public void Extract_ListOrGet_ListAllOrGet_Matches()
    {
        var description = "List all certificates in your Key Vault or get a specific certificate by name.";

        var group = result(description, "list_or_get");
        Assert.Equal("list_or_get", group.Type);
        Assert.NotNull(group.Description);
    }

    [Fact]
    public void Extract_ListOrGet_NoSwitchParam_ReturnsEmptyParams()
    {
        var description = "List all keys in your Key Vault or retrieve a specific key by name.";

        var group = result(description, "list_or_get");
        Assert.Empty(group.Parameters);
    }

    // --- create_or_update ---

    [Fact]
    public void Extract_CreateOrUpdate_Matches()
    {
        var description = "Create or Update a Consumer Group. This tool will either create a Consumer Group resource or update a pre-existing one.";

        var group = result(description, "create_or_update");
        Assert.Equal("create_or_update", group.Type);
        Assert.Empty(group.Parameters);
    }

    [Fact]
    public void Extract_CreateOrUpdate_AlreadyExistsPattern_Matches()
    {
        var description = "Create a secret. If the secret already exists, this tool creates a new version of the secret.";

        var group = result(description, "create_or_update");
        Assert.Equal("create_or_update", group.Type);
    }

    // --- either_or_param ---

    [Fact]
    public void Extract_EitherOrParam_AcceptsEither_Matches()
    {
        var description = "Imports a certificate. Accepts either a file path via --certificate or base64 data via --certificate-data.";

        var group = result(description, "either_or_param");
        Assert.Contains("--certificate", group.Parameters);
        Assert.Contains("--certificate-data", group.Parameters);
    }

    // --- multiple patterns on one tool ---

    [Fact]
    public void Extract_MultiplePatterns_ReturnsAll()
    {
        var description = "List all secrets or get a specific secret. Requires at least one of --vault or --subscription.";

        var groups = _extractor.Extract(description);
        Assert.Contains(groups, g => g.Type == "requires_at_least_one");
        Assert.Contains(groups, g => g.Type == "list_or_get");
    }

    // --- real-world descriptions ---

    [Fact]
    public void Extract_PricingGet_RealDescription()
    {
        var description = "Get Azure retail prices for SKUs and services. Requires at least one filter: --sku, --service, --region, --service-family, or --filter.";

        var group = result(description, "requires_at_least_one");
        Assert.Equal(5, group.Parameters.Count);
    }

    [Fact]
    public void Extract_KeyvaultSecretGet_RealDescription()
    {
        var description = "List all secrets in your Key Vault or get a specific secret by name. Shows all secret names in the vault (without values) or retrieves the secret value along with its full details.";

        var group = result(description, "list_or_get");
        Assert.Equal("list_or_get", group.Type);
    }

    private ConditionalParameterGroup result(string description, string expectedType)
    {
        var groups = _extractor.Extract(description);
        var group = groups.FirstOrDefault(g => g.Type == expectedType);
        Assert.NotNull(group);
        return group;
    }
}
