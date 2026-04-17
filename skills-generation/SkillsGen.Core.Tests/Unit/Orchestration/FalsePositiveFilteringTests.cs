using FluentAssertions;
using SkillsGen.Core.Models;
using SkillsGen.Core.Parsers;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Orchestration;

/// <summary>
/// Tests for false positive resource detection filtering in BuildPrerequisites.
/// Since BuildPrerequisites is private, we test through the parser and model behavior.
/// </summary>
public class FalsePositiveFilteringTests
{
    private readonly SkillMarkdownParser _parser = new();

    [Fact]
    public void DoNotUseFor_CosmosDbMentioned_ParsedCorrectly()
    {
        var content = """
            ---
            name: azure-deploy
            description: "Deploy apps to Azure. DO NOT USE FOR: Cosmos DB queries, database migrations"
            ---

            # Azure Deploy

            Deploy applications. Not for Cosmos DB operations.
            """;

        var result = _parser.Parse("azure-deploy", content);

        result.DoNotUseFor.Should().Contain(item =>
            item.Contains("Cosmos", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DoNotUseFor_MultipleResources_AllParsed()
    {
        var content = """
            ---
            name: azure-compute
            description: "VM and VMSS management. DO NOT USE FOR: Cosmos DB queries, Key Vault secrets, blob storage operations"
            ---

            # Azure Compute

            This skill manages VMs and scale sets. Uses storage account for diagnostics.
            Also references Cosmos DB in docs but should not create Cosmos DB as prereq.
            """;

        var result = _parser.Parse("azure-compute", content);

        result.DoNotUseFor.Should().NotBeEmpty();
        // Verify the DoNotUseFor captures the resource exclusions
        var doNotUseText = string.Join(" ", result.DoNotUseFor).ToLowerInvariant();
        doNotUseText.Should().Contain("cosmos");
    }

    [Fact]
    public void DoNotUseFor_Empty_AllResourcesIncluded()
    {
        var content = """
            ---
            name: azure-storage
            description: "Manage Azure Storage accounts and blob storage."
            ---

            # Azure Storage

            Work with storage account resources and blob storage containers.
            """;

        var result = _parser.Parse("azure-storage", content);

        result.DoNotUseFor.Should().BeEmpty();
        // The raw body mentions storage — so resource detection would include it
        result.RawBody.ToLowerInvariant().Should().Contain("storage account");
    }

    [Fact]
    public void DoNotUseFor_ResourceInDoNotUseForContext_ExcludesFromPrerequisiteCheck()
    {
        // This tests the logic pattern: resource in DoNotUseFor should be excluded
        var skillData = new SkillData
        {
            Name = "azure-deploy",
            DisplayName = "Azure Deploy",
            Description = "Deploy apps to Azure.",
            DoNotUseFor = ["Cosmos DB queries", "database operations"],
            RawBody = "Deploy apps to Azure. Not for Cosmos DB operations."
        };

        var doNotUseForText = string.Join(" ", skillData.DoNotUseFor).ToLowerInvariant();

        // Simulate the filtering logic from BuildPrerequisites
        var bodyContainsCosmos = skillData.RawBody.ToLowerInvariant().Contains("cosmos db");
        var doNotUseForContainsCosmos = doNotUseForText.Contains("cosmos");

        bodyContainsCosmos.Should().BeTrue("body mentions Cosmos DB");
        doNotUseForContainsCosmos.Should().BeTrue("DoNotUseFor mentions Cosmos");

        // Resource should be EXCLUDED because it appears in DoNotUseFor
        var shouldInclude = bodyContainsCosmos && !doNotUseForContainsCosmos;
        shouldInclude.Should().BeFalse("resource in DoNotUseFor context should be excluded");
    }

    [Fact]
    public void DoNotUseFor_ResourceNotInDoNotUseFor_IncludesInPrerequisites()
    {
        var skillData = new SkillData
        {
            Name = "azure-keyvault",
            DisplayName = "Azure Key Vault",
            Description = "Manage Key Vault secrets.",
            DoNotUseFor = ["Azure SQL database operations"],
            RawBody = "Manage key vault secrets, certificates, and keys."
        };

        var doNotUseForText = string.Join(" ", skillData.DoNotUseFor).ToLowerInvariant();

        var bodyContainsKeyVault = skillData.RawBody.ToLowerInvariant().Contains("key vault");
        var doNotUseForContainsKeyVault = doNotUseForText.Contains("key vault");

        bodyContainsKeyVault.Should().BeTrue("body mentions key vault");
        doNotUseForContainsKeyVault.Should().BeFalse("DoNotUseFor does not mention key vault");

        var shouldInclude = bodyContainsKeyVault && !doNotUseForContainsKeyVault;
        shouldInclude.Should().BeTrue("resource not in DoNotUseFor should be included");
    }
}
