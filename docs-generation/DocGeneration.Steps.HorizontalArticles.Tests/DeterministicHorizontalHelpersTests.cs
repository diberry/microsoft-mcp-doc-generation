// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CSharpGenerator.Models;
using HorizontalArticleGenerator.Generators;
using HorizontalArticleGenerator.Models;
using NUnit.Framework;

namespace HorizontalArticleGenerator.Tests;

[TestFixture]
public class DeterministicHorizontalHelpersTests
{
    // ── ClassifyToolPlane ────────────────────────────────────────────

    [Test]
    public void ClassifyToolPlane_ReadOnlyMetadata_ReturnsData()
    {
        var tool = MakeTool("keyvault secret get", readOnly: true);
        Assert.That(DeterministicHorizontalHelpers.ClassifyToolPlane(tool), Is.EqualTo("data"));
    }

    [Test]
    public void ClassifyToolPlane_DestructiveMetadata_ReturnsManagement()
    {
        var tool = MakeTool("sql database delete", destructive: true);
        Assert.That(DeterministicHorizontalHelpers.ClassifyToolPlane(tool), Is.EqualTo("management"));
    }

    [TestCase("storage account create", "management")]
    [TestCase("keyvault secret delete", "management")]
    [TestCase("appservice webapp update", "management")]
    [TestCase("storage account list", "data")]
    [TestCase("cosmos container get", "data")]
    public void ClassifyToolPlane_ByCommandVerb(string command, string expectedPlane)
    {
        var tool = MakeTool(command);
        Assert.That(DeterministicHorizontalHelpers.ClassifyToolPlane(tool), Is.EqualTo(expectedPlane));
    }

    [Test]
    public void ClassifyToolPlane_NoMetadataNoVerb_DefaultsToData()
    {
        var tool = MakeTool("monitor query");
        Assert.That(DeterministicHorizontalHelpers.ClassifyToolPlane(tool), Is.EqualTo("data"));
    }

    // ── OrderToolsByPlane ───────────────────────────────────────────

    [Test]
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
        Assert.That(ordered[0].Command, Does.Contain("create"));
        Assert.That(ordered[1].Command, Does.Contain("create"));
        Assert.That(ordered[2].Command, Does.Contain("get"));
        Assert.That(ordered[3].Command, Does.Contain("get"));
    }

    [Test]
    public void OrderToolsByPlane_AlphabeticalWithinPlane()
    {
        var tools = new List<HorizontalToolSummary>
        {
            MakeTool("storage table list", readOnly: true),
            MakeTool("storage account list", readOnly: true),
            MakeTool("storage blob list", readOnly: true),
        };

        var ordered = DeterministicHorizontalHelpers.OrderToolsByPlane(tools);

        Assert.That(ordered[0].Command, Is.EqualTo("storage account list"));
        Assert.That(ordered[1].Command, Is.EqualTo("storage blob list"));
        Assert.That(ordered[2].Command, Is.EqualTo("storage table list"));
    }

    [Test]
    public void OrderToolsByPlane_PreservesAllTools()
    {
        var tools = new List<HorizontalToolSummary>
        {
            MakeTool("a list", readOnly: true),
            MakeTool("b create"),
            MakeTool("c get", readOnly: true),
        };

        var ordered = DeterministicHorizontalHelpers.OrderToolsByPlane(tools);
        Assert.That(ordered, Has.Count.EqualTo(3));
    }

    // ── ExtractCapability ───────────────────────────────────────────

    [Test]
    public void ExtractCapability_FromDescription_ReturnsCleanCapability()
    {
        var tool = MakeTool("storage account list");
        tool.Description = "List all storage accounts in a subscription.";

        var capability = DeterministicHorizontalHelpers.ExtractCapability(tool);

        Assert.That(capability, Does.Not.EndWith("."));
        Assert.That(capability, Is.Not.Empty);
    }

    [Test]
    public void ExtractCapability_StripsTrailingPeriod()
    {
        var tool = MakeTool("keyvault secret create");
        tool.Description = "Create a new secret in a key vault.";

        var capability = DeterministicHorizontalHelpers.ExtractCapability(tool);

        Assert.That(capability, Does.Not.EndWith("."));
    }

    [Test]
    public void ExtractCapability_TruncatesLongDescription()
    {
        var tool = MakeTool("deploy generate_plan");
        tool.Description = "Generate a detailed deployment plan that covers all the infrastructure and code changes needed for deploying the application to Azure with multiple steps and stages.";

        var capability = DeterministicHorizontalHelpers.ExtractCapability(tool);

        var wordCount = capability.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.That(wordCount, Is.LessThanOrEqualTo(15));
    }

    // ── TruncateDescription ─────────────────────────────────────────

    [Test]
    public void TruncateDescription_ShortEnough_ReturnsAsIs()
    {
        var result = DeterministicHorizontalHelpers.TruncateDescription(
            "List all storage accounts", maxWords: 10);
        Assert.That(result, Is.EqualTo("List all storage accounts"));
    }

    [Test]
    public void TruncateDescription_TooLong_TruncatesAndAddsSuffix()
    {
        var result = DeterministicHorizontalHelpers.TruncateDescription(
            "Generate a very detailed and comprehensive deployment plan for Azure resources", maxWords: 5);

        var wordCount = result.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.That(wordCount, Is.LessThanOrEqualTo(6)); // 5 words + possible suffix
    }

    // ── PreComputeCapabilities ──────────────────────────────────────

    [Test]
    public void PreComputeCapabilities_OnePerTool()
    {
        var tools = new List<HorizontalToolSummary>
        {
            MakeTool("storage account list", description: "List all storage accounts."),
            MakeTool("storage account create", description: "Create a new storage account."),
            MakeTool("storage blob get", description: "Get blob properties."),
        };

        var capabilities = DeterministicHorizontalHelpers.PreComputeCapabilities(tools);

        Assert.That(capabilities, Has.Count.EqualTo(3));
        Assert.That(capabilities, Has.All.Not.EndsWith("."));
    }

    // ── PreComputeShortDescriptions ─────────────────────────────────

    [Test]
    public void PreComputeShortDescriptions_ReturnsMapByCommand()
    {
        var tools = new List<HorizontalToolSummary>
        {
            MakeTool("storage account list", description: "List all storage accounts in a subscription."),
            MakeTool("storage blob get", description: "Get properties for a specific blob in a container."),
        };

        var descriptions = DeterministicHorizontalHelpers.PreComputeShortDescriptions(tools);

        Assert.That(descriptions, Has.Count.EqualTo(2));
        Assert.That(descriptions.ContainsKey("storage account list"), Is.True);
        Assert.That(descriptions.ContainsKey("storage blob get"), Is.True);
    }

    [Test]
    public void PreComputeShortDescriptions_MaxTenToFifteenWords()
    {
        var tools = new List<HorizontalToolSummary>
        {
            MakeTool("deploy plan", description: "Generate a very detailed and comprehensive deployment plan covering all infrastructure and code changes needed for deploying the application."),
        };

        var descriptions = DeterministicHorizontalHelpers.PreComputeShortDescriptions(tools);
        var words = descriptions["deploy plan"].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        Assert.That(words, Is.LessThanOrEqualTo(15));
    }

    // ── Idempotent ──────────────────────────────────────────────────

    [Test]
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

        Assert.That(order1.Select(t => t.Command), Is.EqualTo(order2.Select(t => t.Command)));
        Assert.That(caps1, Is.EqualTo(caps2));
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
