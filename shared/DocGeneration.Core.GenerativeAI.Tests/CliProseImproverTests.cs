// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace GenerativeAI.Tests;

/// <summary>
/// Tests for CliProseImprover — deterministic NLP→CLI voice adaptation (no AI).
/// </summary>
public class CliProseImproverTests
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static CliToolInfo MakeTool(
        string command = "storage account list",
        string description = "List all storage accounts in a subscription.",
        params CliSwitch[] switches)
    {
        return new CliToolInfo(command, description, switches.Length > 0 ? switches : new[]
        {
            new CliSwitch("--subscription", "The Azure subscription ID."),
            new CliSwitch("--resource-group", "The name of the resource group.")
        });
    }

    private static IReadOnlyDictionary<string, CliToolInfo> MakeToolDict(params (string key, CliToolInfo tool)[] items)
    {
        var result = new Dictionary<string, CliToolInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, tool) in items)
            result[key] = tool;
        return result;
    }

    private static CliProseImprover CreateImprover()
        => new();

    // ── ImproveProseAsync tests ──────────────────────────────────────

    [Fact]
    public async Task ImproveProseAsync_WithNlpDescription_AdaptsVoiceDeterministically()
    {
        var nlpDesc = "This tool retrieves detailed information about Azure Storage accounts, including account name and location.";
        var improver = CreateImprover();
        var nlpDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage account list"] = nlpDesc
        };
        var tools = MakeToolDict(("storage account list", MakeTool()));

        var result = await improver.ImproveProseAsync(tools, nlpDescriptions);

        Assert.Single(result);
        var tool = result.Values.First();
        Assert.Equal("Retrieves detailed information about Azure Storage accounts, including account name and location.", tool.Description);
    }

    [Fact]
    public async Task ImproveProseAsync_WithoutNlpDescription_KeepsRawDescription()
    {
        var improver = CreateImprover();
        var rawTool = MakeTool();
        var tools = MakeToolDict(("storage account list", rawTool));

        var result = await improver.ImproveProseAsync(tools, nlpDescriptions: null);

        Assert.Single(result);
        var tool = result.Values.First();
        Assert.Equal(rawTool.Description, tool.Description);
    }

    [Fact]
    public async Task ImproveProseAsync_SwitchDescriptionsPreservedAsIs()
    {
        var nlpDesc = "This tool lists storage accounts.";
        var improver = CreateImprover();
        var rawTool = MakeTool();
        var nlpDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage account list"] = nlpDesc
        };
        var tools = MakeToolDict(("storage account list", rawTool));

        var result = await improver.ImproveProseAsync(tools, nlpDescriptions);

        var tool = result.Values.First();
        Assert.Equal(rawTool.Switches[0].Description, tool.Switches[0].Description);
        Assert.Equal(rawTool.Switches[1].Description, tool.Switches[1].Description);
    }

    [Fact]
    public async Task ImproveProseAsync_SwitchNamesPreserved()
    {
        var improver = CreateImprover();
        var tool = MakeTool();
        var tools = MakeToolDict(("storage account list", tool));

        var result = await improver.ImproveProseAsync(tools);

        var improved = result.Values.First();
        Assert.Equal("--subscription", improved.Switches[0].Name);
        Assert.Equal("--resource-group", improved.Switches[1].Name);
    }

    [Fact]
    public async Task ImproveProseAsync_EmptyToolDict_ReturnsEmpty()
    {
        var improver = CreateImprover();
        var tools = new Dictionary<string, CliToolInfo>(StringComparer.OrdinalIgnoreCase);

        var result = await improver.ImproveProseAsync(tools);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ImproveProseAsync_MultipleTools_AllProcessed()
    {
        var improver = CreateImprover();
        var nlpDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage account list"] = "This tool lists all storage accounts.",
            ["storage account show"] = "This tool shows a specific storage account."
        };
        var tool1 = new CliToolInfo("storage account list", "List accounts.", new[]
        {
            new CliSwitch("--subscription", "The Azure subscription ID.")
        });
        var tool2 = new CliToolInfo("storage account show", "Show an account.", new[]
        {
            new CliSwitch("--subscription", "The Azure subscription ID.")
        });
        var tools = MakeToolDict(
            ("storage account list", tool1),
            ("storage account show", tool2));

        var result = await improver.ImproveProseAsync(tools, nlpDescriptions);

        Assert.Equal(2, result.Count);
        Assert.Equal("Lists all storage accounts.", result["storage account list"].Description);
        Assert.Equal("Shows a specific storage account.", result["storage account show"].Description);
    }

    [Fact]
    public async Task ImproveProseAsync_NlpMissingSomeTool_MixesAdaptedAndRaw()
    {
        var improver = CreateImprover();
        var nlpDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage account list"] = "This tool lists all storage accounts."
        };
        var tool1 = MakeTool(command: "storage account list", description: "Raw CLI list desc.");
        var tool2 = MakeTool(command: "storage account show", description: "Raw CLI show desc.");
        var tools = MakeToolDict(
            ("storage account list", tool1),
            ("storage account show", tool2));

        var result = await improver.ImproveProseAsync(tools, nlpDescriptions);

        Assert.Equal("Lists all storage accounts.", result["storage account list"].Description);
        Assert.Equal("Raw CLI show desc.", result["storage account show"].Description);
    }

    [Fact]
    public async Task ImproveProseAsync_EmptyNlpDescription_KeepsRawDescription()
    {
        var improver = CreateImprover();
        var nlpDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage account list"] = "   "
        };
        var rawTool = MakeTool();
        var tools = MakeToolDict(("storage account list", rawTool));

        var result = await improver.ImproveProseAsync(tools, nlpDescriptions);

        Assert.Equal(rawTool.Description, result.Values.First().Description);
    }

    // ── AdaptNlpToCliVoice tests ─────────────────────────────────────

    [Fact]
    public void AdaptNlpToCliVoice_RemovesThisToolPrefix()
    {
        var result = CliProseImprover.AdaptNlpToCliVoice("This tool creates a storage account.");
        Assert.Equal("Creates a storage account.", result);
    }

    [Fact]
    public void AdaptNlpToCliVoice_RemovesMcpPreamble()
    {
        var result = CliProseImprover.AdaptNlpToCliVoice(
            "Model Context Protocol (MCP) tools let you run tasks that manage Azure resources. Creates a storage account.");
        Assert.Equal("Creates a storage account.", result);
    }

    [Fact]
    public void AdaptNlpToCliVoice_ReplacesMcpServerReference()
    {
        var result = CliProseImprover.AdaptNlpToCliVoice("Gets details from the MCP Server about accounts.");
        Assert.Equal("Gets details from the Azure MCP CLI about accounts.", result);
    }

    [Fact]
    public void AdaptNlpToCliVoice_NoChangeNeeded_ReturnsAsIs()
    {
        var result = CliProseImprover.AdaptNlpToCliVoice("Creates a storage account in the specified region.");
        Assert.Equal("Creates a storage account in the specified region.", result);
    }

    [Fact]
    public void AdaptNlpToCliVoice_CapitalizesAfterStripping()
    {
        // "this tool creates..." → "Creates..." (lowercase 'c' in original becomes uppercase)
        var result = CliProseImprover.AdaptNlpToCliVoice("this tool retrieves data.");
        Assert.Equal("Retrieves data.", result);
    }
}
