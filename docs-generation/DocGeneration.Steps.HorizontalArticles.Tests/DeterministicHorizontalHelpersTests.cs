// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CSharpGenerator.Models;
using HorizontalArticleGenerator.Generators;
using HorizontalArticleGenerator.Models;
using Xunit;

namespace HorizontalArticleGenerator.Tests;

public class DeterministicHorizontalHelpersTests
{
    // ── ClassifyToolPlane ────────────────────────────────────────────

    [Fact]
    public void ClassifyToolPlane_ReadOnlyMetadata_ReturnsData()
    {
        var tool = MakeTool("keyvault secret get", readOnly: true);
        Assert.Equal("data", DeterministicHorizontalHelpers.ClassifyToolPlane(tool));
    }

    [Fact]
    public void ClassifyToolPlane_DestructiveMetadata_ReturnsManagement()
    {
        var tool = MakeTool("sql database delete", destructive: true);
        Assert.Equal("management", DeterministicHorizontalHelpers.ClassifyToolPlane(tool));
    }

    [Theory]
    [InlineData("storage account create", "management")]
    [InlineData("keyvault secret delete", "management")]
    [InlineData("appservice webapp update", "management")]
    [InlineData("storage account list", "data")]
    [InlineData("cosmos container get", "data")]
    public void ClassifyToolPlane_ByCommandVerb(string command, string expectedPlane)
    {
        var tool = MakeTool(command);
        Assert.Equal(expectedPlane, DeterministicHorizontalHelpers.ClassifyToolPlane(tool));
    }

    [Fact]
    public void ClassifyToolPlane_NoMetadataNoVerb_DefaultsToData()
    {
        var tool = MakeTool("monitor query");
        Assert.Equal("data", DeterministicHorizontalHelpers.ClassifyToolPlane(tool));
    }

    // ── OrderToolsByPlane ───────────────────────────────────────────

    [Fact]
    public void OrderToolsByPlane_ManagementBeforeData()
    {
        var tools = new List<HorizontalToolSummary>
        {
            MakeTool("keyvault secret get", readOnly: true),
            MakeTool("keyvault secret create", destructive: false),
            MakeTool("keyvault key get", readOnly: true),
            MakeTool("keyvault key create", destructive: false),
        };

        var ordered = DeterministicHorizontalHelpers.OrderToolsByPlane(tools);

        // Management (create) first, then data (get)
        Assert.Contains("create", ordered[0].Command);
        Assert.Contains("create", ordered[1].Command);
        Assert.Contains("get", ordered[2].Command);
        Assert.Contains("get", ordered[3].Command);
    }

    [Fact]
    public void OrderToolsByPlane_AlphabeticalWithinPlane()
    {
        var tools = new List<HorizontalToolSummary>
        {
            MakeTool("storage table list", readOnly: true),
            MakeTool("storage account list", readOnly: true),
            MakeTool("storage blob list", readOnly: true),
        };

        var ordered = DeterministicHorizontalHelpers.OrderToolsByPlane(tools);

        Assert.Equal("storage account list", ordered[0].Command);
        Assert.Equal("storage blob list", ordered[1].Command);
        Assert.Equal("storage table list", ordered[2].Command);
    }

    [Fact]
    public void OrderToolsByPlane_PreservesAllTools()
    {
        var tools = new List<HorizontalToolSummary>
        {
            MakeTool("a list", readOnly: true),
            MakeTool("b create"),
            MakeTool("c get", readOnly: true),
        };

        var ordered = DeterministicHorizontalHelpers.OrderToolsByPlane(tools);
        Assert.Equal(3, ordered.Count);
    }

    // ── ExtractCapability ───────────────────────────────────────────

    [Fact]
    public void ExtractCapability_FromDescription_ReturnsCleanCapability()
    {
        var tool = MakeTool("storage account list");
        tool.Description = "List all storage accounts in a subscription.";

        var capability = DeterministicHorizontalHelpers.ExtractCapability(tool);

        Assert.False(capability.EndsWith("."));
        Assert.NotEmpty(capability);
    }

    [Fact]
    public void ExtractCapability_StripsTrailingPeriod()
    {
        var tool = MakeTool("keyvault secret create");
        tool.Description = "Create a new secret in a key vault.";

        var capability = DeterministicHorizontalHelpers.ExtractCapability(tool);

        Assert.False(capability.EndsWith("."));
    }

    [Fact]
    public void ExtractCapability_TruncatesLongDescription()
    {
        var tool = MakeTool("deploy generate_plan");
        tool.Description = "Generate a detailed deployment plan that covers all the infrastructure and code changes needed for deploying the application to Azure with multiple steps and stages.";

        var capability = DeterministicHorizontalHelpers.ExtractCapability(tool);

        var wordCount = capability.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.True(wordCount <= 15);
    }

    // ── TruncateDescription ─────────────────────────────────────────

    [Fact]
    public void TruncateDescription_ShortEnough_ReturnsAsIs()
    {
        var result = DeterministicHorizontalHelpers.TruncateDescription(
            "List all storage accounts", maxWords: 10);
        Assert.Equal("List all storage accounts", result);
    }

    [Fact]
    public void TruncateDescription_TooLong_TruncatesAndAddsSuffix()
    {
        var result = DeterministicHorizontalHelpers.TruncateDescription(
            "Generate a very detailed and comprehensive deployment plan for Azure resources", maxWords: 5);

        var wordCount = result.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.True(wordCount <= 6); // 5 words + possible suffix
    }

    // ── PreComputeCapabilities ──────────────────────────────────────

    [Fact]
    public void PreComputeCapabilities_OnePerTool()
    {
        var tools = new List<HorizontalToolSummary>
        {
            MakeTool("storage account list", description: "List all storage accounts."),
            MakeTool("storage account create", description: "Create a new storage account."),
            MakeTool("storage blob get", description: "Get blob properties."),
        };

        var capabilities = DeterministicHorizontalHelpers.PreComputeCapabilities(tools);

        Assert.Equal(3, capabilities.Count);
        Assert.All(capabilities.Values, v => Assert.False(v.EndsWith(".")));
    }

    // ── PreComputeShortDescriptions ─────────────────────────────────

    [Fact]
    public void PreComputeShortDescriptions_ReturnsMapByCommand()
    {
        var tools = new List<HorizontalToolSummary>
        {
            MakeTool("storage account list", description: "List all storage accounts in a subscription."),
            MakeTool("storage blob get", description: "Get properties for a specific blob in a container."),
        };

        var descriptions = DeterministicHorizontalHelpers.PreComputeShortDescriptions(tools);

        Assert.Equal(2, descriptions.Count);
        Assert.True(descriptions.ContainsKey("storage account list"));
        Assert.True(descriptions.ContainsKey("storage blob get"));
    }

    [Fact]
    public void PreComputeShortDescriptions_MaxTenToFifteenWords()
    {
        var tools = new List<HorizontalToolSummary>
        {
            MakeTool("deploy plan", description: "Generate a very detailed and comprehensive deployment plan covering all infrastructure and code changes needed for deploying the application."),
        };

        var descriptions = DeterministicHorizontalHelpers.PreComputeShortDescriptions(tools);
        var words = descriptions["deploy plan"].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        Assert.True(words <= 15);
    }

    // ── Idempotent ──────────────────────────────────────────────────

    [Fact]
    public void OrderAndCapabilities_SameInput_SameOutput()
    {
        var tools = new List<HorizontalToolSummary>
        {
            MakeTool("keyvault secret get", readOnly: true, description: "Get a secret."),
            MakeTool("keyvault secret create", description: "Create a secret."),
        };

        var order1 = DeterministicHorizontalHelpers.OrderToolsByPlane(tools);
        var caps1 = DeterministicHorizontalHelpers.PreComputeCapabilities(tools);
        var order2 = DeterministicHorizontalHelpers.OrderToolsByPlane(tools);
        var caps2 = DeterministicHorizontalHelpers.PreComputeCapabilities(tools);

        Assert.Equal(order1.Select(t => t.Command), order2.Select(t => t.Command));
        Assert.Equal(caps1.Values, caps2.Values);
    }

    // ── Edge cases (review feedback) ────────────────────────────────

    [Fact]
    public void ClassifyToolPlane_NullMetadata_DefaultsToVerbClassification()
    {
        var tool = new HorizontalToolSummary
        {
            Command = "storage account create",
            Description = "Create account",
            Metadata = null!
        };
        Assert.Equal("management", DeterministicHorizontalHelpers.ClassifyToolPlane(tool));
    }

    [Fact]
    public void ClassifyToolPlane_EmptyMetadata_DefaultsToVerbClassification()
    {
        var tool = new HorizontalToolSummary
        {
            Command = "cosmos container get",
            Description = "Get container",
            Metadata = new Dictionary<string, MetadataValue>()
        };
        Assert.Equal("data", DeterministicHorizontalHelpers.ClassifyToolPlane(tool));
    }

    [Theory]
    [InlineData("appconfig createorupdate", "management")]
    [InlineData("compute deploy", "management")]
    [InlineData("functionapp publish", "management")]
    public void ClassifyToolPlane_CompoundVerbs_CorrectlyClassified(string command, string expected)
    {
        var tool = MakeTool(command);
        Assert.Equal(expected, DeterministicHorizontalHelpers.ClassifyToolPlane(tool));
    }

    [Fact]
    public void OrderToolsByPlane_EmptyList_ReturnsEmpty()
    {
        var result = DeterministicHorizontalHelpers.OrderToolsByPlane(new List<HorizontalToolSummary>());
        Assert.Empty(result);
    }

    [Fact]
    public void PreComputeCapabilities_DuplicateCommands_FirstWins()
    {
        var tools = new List<HorizontalToolSummary>
        {
            MakeTool("storage list", description: "First description."),
            MakeTool("storage list", description: "Second description."),
        };

        var caps = DeterministicHorizontalHelpers.PreComputeCapabilities(tools);
        Assert.Equal(1, caps.Count);
        Assert.Equal("First description", caps["storage list"]);
    }

    [Fact]
    public void TruncateDescription_EmptyString_ReturnsEmpty()
    {
        var result = DeterministicHorizontalHelpers.TruncateDescription("", maxWords: 10);
        Assert.Equal("", result);
    }

    [Fact]
    public void TruncateDescription_SingleWord_ReturnsTrimmed()
    {
        var result = DeterministicHorizontalHelpers.TruncateDescription("List.", maxWords: 10);
        Assert.Equal("List", result);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static HorizontalToolSummary MakeTool(
        string command,
        bool readOnly = false,
        bool destructive = false,
        string? description = null)
    {
        return new HorizontalToolSummary
        {
            Command = command,
            Description = description ?? $"Test description for {command}",
            ParameterCount = 2,
            Metadata = new Dictionary<string, MetadataValue>
            {
                ["readOnly"] = new MetadataValue { Value = readOnly },
                ["destructive"] = new MetadataValue { Value = destructive },
                ["secret"] = new MetadataValue { Value = false },
            }
        };
    }
}
