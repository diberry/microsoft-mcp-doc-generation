// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Regression tests validating that ToolOrderingPolicy produces bitwise-identical
/// ordering to the previous inline sort logic against a realistic tool corpus.
/// Covers 10+ single-resource families and 3+ multi-resource families (#521).
/// </summary>
public class ToolOrderingPolicyRegressionTests
{
    #region Single-Resource Corpus

    /// <summary>
    /// Realistic corpus of 10 single-resource families with 3-8 tools each (55 tools total).
    /// Expected order is defined by the OLD inline sort logic:
    /// OrderBy(ToolName, OrdinalIgnoreCase).ThenBy(ToolName, Ordinal).ThenBy(FileName, Ordinal)
    /// </summary>
    private static readonly Dictionary<string, List<ToolContent>> SingleResourceFamilies = new()
    {
        ["monitor"] = new()
        {
            MakeSingle("monitor alert create", "monitor-alert-create.md", "monitor"),
            MakeSingle("monitor alert delete", "monitor-alert-delete.md", "monitor"),
            MakeSingle("monitor alert list", "monitor-alert-list.md", "monitor"),
            MakeSingle("monitor metric list", "monitor-metric-list.md", "monitor"),
            MakeSingle("monitor metric query", "monitor-metric-query.md", "monitor"),
        },
        ["compute"] = new()
        {
            MakeSingle("compute disk create", "compute-disk-create.md", "compute"),
            MakeSingle("compute disk delete", "compute-disk-delete.md", "compute"),
            MakeSingle("compute disk list", "compute-disk-list.md", "compute"),
            MakeSingle("compute vm create", "compute-vm-create.md", "compute"),
            MakeSingle("compute vm deallocate", "compute-vm-deallocate.md", "compute"),
            MakeSingle("compute vm delete", "compute-vm-delete.md", "compute"),
            MakeSingle("compute vm list", "compute-vm-list.md", "compute"),
            MakeSingle("compute vm start", "compute-vm-start.md", "compute"),
        },
        ["storage"] = new()
        {
            MakeSingle("storage account create", "storage-account-create.md", "storage"),
            MakeSingle("storage account delete", "storage-account-delete.md", "storage"),
            MakeSingle("storage account list", "storage-account-list.md", "storage"),
            MakeSingle("storage blob delete", "storage-blob-delete.md", "storage"),
            MakeSingle("storage blob list", "storage-blob-list.md", "storage"),
            MakeSingle("storage blob upload", "storage-blob-upload.md", "storage"),
        },
        ["network"] = new()
        {
            MakeSingle("network nsg create", "network-nsg-create.md", "network"),
            MakeSingle("network nsg delete", "network-nsg-delete.md", "network"),
            MakeSingle("network nsg list", "network-nsg-list.md", "network"),
            MakeSingle("network vnet create", "network-vnet-create.md", "network"),
            MakeSingle("network vnet delete", "network-vnet-delete.md", "network"),
            MakeSingle("network vnet list", "network-vnet-list.md", "network"),
            MakeSingle("network vnet show", "network-vnet-show.md", "network"),
        },
        ["keyvault"] = new()
        {
            MakeSingle("keyvault certificate create", "keyvault-certificate-create.md", "keyvault"),
            MakeSingle("keyvault certificate delete", "keyvault-certificate-delete.md", "keyvault"),
            MakeSingle("keyvault certificate list", "keyvault-certificate-list.md", "keyvault"),
            MakeSingle("keyvault key create", "keyvault-key-create.md", "keyvault"),
            MakeSingle("keyvault key list", "keyvault-key-list.md", "keyvault"),
            MakeSingle("keyvault secret list", "keyvault-secret-list.md", "keyvault"),
            MakeSingle("keyvault secret set", "keyvault-secret-set.md", "keyvault"),
        },
        ["cosmos"] = new()
        {
            MakeSingle("cosmos container create", "cosmos-container-create.md", "cosmos"),
            MakeSingle("cosmos container delete", "cosmos-container-delete.md", "cosmos"),
            MakeSingle("cosmos container list", "cosmos-container-list.md", "cosmos"),
            MakeSingle("cosmos database create", "cosmos-database-create.md", "cosmos"),
            MakeSingle("cosmos database list", "cosmos-database-list.md", "cosmos"),
        },
        ["sql"] = new()
        {
            MakeSingle("sql database create", "sql-database-create.md", "sql"),
            MakeSingle("sql database delete", "sql-database-delete.md", "sql"),
            MakeSingle("sql database list", "sql-database-list.md", "sql"),
            MakeSingle("sql server create", "sql-server-create.md", "sql"),
            MakeSingle("sql server list", "sql-server-list.md", "sql"),
        },
        ["aks"] = new()
        {
            MakeSingle("aks cluster create", "aks-cluster-create.md", "aks"),
            MakeSingle("aks cluster delete", "aks-cluster-delete.md", "aks"),
            MakeSingle("aks cluster list", "aks-cluster-list.md", "aks"),
            MakeSingle("aks nodepool add", "aks-nodepool-add.md", "aks"),
            MakeSingle("aks nodepool delete", "aks-nodepool-delete.md", "aks"),
            MakeSingle("aks nodepool list", "aks-nodepool-list.md", "aks"),
        },
        ["appservice"] = new()
        {
            MakeSingle("appservice plan create", "appservice-plan-create.md", "appservice"),
            MakeSingle("appservice plan delete", "appservice-plan-delete.md", "appservice"),
            MakeSingle("appservice plan list", "appservice-plan-list.md", "appservice"),
            MakeSingle("appservice webapp create", "appservice-webapp-create.md", "appservice"),
            MakeSingle("appservice webapp delete", "appservice-webapp-delete.md", "appservice"),
            MakeSingle("appservice webapp list", "appservice-webapp-list.md", "appservice"),
            MakeSingle("appservice webapp restart", "appservice-webapp-restart.md", "appservice"),
            MakeSingle("appservice webapp stop", "appservice-webapp-stop.md", "appservice"),
        },
        ["eventhubs"] = new()
        {
            MakeSingle("eventhubs hub create", "eventhubs-hub-create.md", "eventhubs"),
            MakeSingle("eventhubs hub delete", "eventhubs-hub-delete.md", "eventhubs"),
            MakeSingle("eventhubs hub list", "eventhubs-hub-list.md", "eventhubs"),
            MakeSingle("eventhubs namespace create", "eventhubs-namespace-create.md", "eventhubs"),
            MakeSingle("eventhubs namespace list", "eventhubs-namespace-list.md", "eventhubs"),
        },
    };

    /// <summary>
    /// Expected order for each family, computed using the OLD inline logic:
    /// OrderBy(ToolName, OrdinalIgnoreCase).ThenBy(ToolName, Ordinal).ThenBy(FileName, Ordinal)
    /// </summary>
    private static List<string> GetExpectedSingleResourceOrder(List<ToolContent> tools)
    {
        return tools
            .OrderBy(t => t.ToolName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.ToolName, StringComparer.Ordinal)
            .ThenBy(t => t.FileName, StringComparer.Ordinal)
            .Select(t => t.ToolName)
            .ToList();
    }

    [Fact]
    public void SingleResource_AllFamilies_MatchOldInlineSortBehavior()
    {
        foreach (var (familyName, tools) in SingleResourceFamilies)
        {
            var expected = GetExpectedSingleResourceOrder(tools);
            var actual = ToolOrderingPolicy.OrderForSingleResource(tools)
                .Select(t => t.ToolName)
                .ToList();

            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }
    }

    [Theory]
    [InlineData("monitor")]
    [InlineData("compute")]
    [InlineData("storage")]
    [InlineData("network")]
    [InlineData("keyvault")]
    [InlineData("cosmos")]
    [InlineData("sql")]
    [InlineData("aks")]
    [InlineData("appservice")]
    [InlineData("eventhubs")]
    public void SingleResource_PerFamily_MatchesExpectedOrder(string familyName)
    {
        var tools = SingleResourceFamilies[familyName];
        var expected = GetExpectedSingleResourceOrder(tools);
        var actual = ToolOrderingPolicy.OrderForSingleResource(tools)
            .Select(t => t.ToolName)
            .ToList();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SingleResource_OrderIsStableAcrossMultipleInvocations()
    {
        foreach (var (_, tools) in SingleResourceFamilies)
        {
            var run1 = ToolOrderingPolicy.OrderForSingleResource(tools)
                .Select(t => t.ToolName).ToList();
            var run2 = ToolOrderingPolicy.OrderForSingleResource(tools)
                .Select(t => t.ToolName).ToList();

            Assert.Equal(run1, run2);
        }
    }

    [Fact]
    public void SingleResource_ShuffledInput_ProducesSameOutput()
    {
        var tools = SingleResourceFamilies["compute"].ToList();
        var shuffled = new List<ToolContent>
        {
            tools[5], tools[2], tools[7], tools[0], tools[4], tools[1], tools[6], tools[3]
        };

        var fromOriginal = ToolOrderingPolicy.OrderForSingleResource(tools)
            .Select(t => t.ToolName).ToList();
        var fromShuffled = ToolOrderingPolicy.OrderForSingleResource(shuffled)
            .Select(t => t.ToolName).ToList();

        Assert.Equal(fromOriginal, fromShuffled);
    }

    [Fact]
    public void SingleResource_CorpusHasAtLeast40Tools()
    {
        var totalTools = SingleResourceFamilies.Values.Sum(f => f.Count);
        Assert.True(totalTools >= 40, $"Corpus has {totalTools} tools, expected at least 40");
    }

    [Fact]
    public void SingleResource_CaseSensitiveTieBreak_IsCorrect()
    {
        // Verify that case-sensitive tie-break works: uppercase sorts before lowercase in Ordinal
        var tools = new List<ToolContent>
        {
            MakeSingle("List items", "list-items-upper.md", "test"),
            MakeSingle("list items", "list-items-lower.md", "test"),
        };

        var ordered = ToolOrderingPolicy.OrderForSingleResource(tools).ToList();

        // In Ordinal comparison, uppercase 'L' (76) < lowercase 'l' (108)
        Assert.Equal("List items", ordered[0].ToolName);
        Assert.Equal("list items", ordered[1].ToolName);
    }

    #endregion

    #region Multi-Resource Corpus

    /// <summary>
    /// Three multi-resource families with 4+ tools each, using realistic Azure CLI patterns.
    /// Multi-resource families sort by ExtractActionVerb(Command).
    /// </summary>
    private static readonly Dictionary<string, List<ToolContent>> MultiResourceFamilies = new()
    {
        ["compute-multi"] = new()
        {
            MakeMulti("VM create", "compute vm create", "vm-create.md", "compute-multi"),
            MakeMulti("VM delete", "compute vm delete", "vm-delete.md", "compute-multi"),
            MakeMulti("VM list", "compute vm list", "vm-list.md", "compute-multi"),
            MakeMulti("VM start", "compute vm start", "vm-start.md", "compute-multi"),
            MakeMulti("Disk create", "compute disk create", "disk-create.md", "compute-multi"),
            MakeMulti("Disk delete", "compute disk delete", "disk-delete.md", "compute-multi"),
            MakeMulti("Disk list", "compute disk list", "disk-list.md", "compute-multi"),
            MakeMulti("VMSS update", "compute vmss update", "vmss-update.md", "compute-multi"),
        },
        ["storage-multi"] = new()
        {
            MakeMulti("Account create", "storage account create", "account-create.md", "storage-multi"),
            MakeMulti("Account delete", "storage account delete", "account-delete.md", "storage-multi"),
            MakeMulti("Account list", "storage account list", "account-list.md", "storage-multi"),
            MakeMulti("Blob upload", "storage blob upload", "blob-upload.md", "storage-multi"),
            MakeMulti("Blob delete", "storage blob delete", "blob-delete.md", "storage-multi"),
            MakeMulti("Blob list", "storage blob list", "blob-list.md", "storage-multi"),
        },
        ["network-multi"] = new()
        {
            MakeMulti("VNet create", "network vnet create", "vnet-create.md", "network-multi"),
            MakeMulti("VNet delete", "network vnet delete", "vnet-delete.md", "network-multi"),
            MakeMulti("VNet list", "network vnet list", "vnet-list.md", "network-multi"),
            MakeMulti("NSG create", "network nsg create", "nsg-create.md", "network-multi"),
            MakeMulti("NSG delete", "network nsg delete", "nsg-delete.md", "network-multi"),
            MakeMulti("NSG list", "network nsg list", "nsg-list.md", "network-multi"),
            MakeMulti("NSG show", "network nsg show", "nsg-show.md", "network-multi"),
        },
    };

    /// <summary>
    /// Expected order for multi-resource families using ExtractActionVerb-based sorting.
    /// </summary>
    private static List<string> GetExpectedMultiResourceOrder(List<ToolContent> tools)
    {
        return tools
            .OrderBy(t => ToolOrderingPolicy.ExtractActionVerb(t.Command), StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => ToolOrderingPolicy.ExtractActionVerb(t.Command), StringComparer.Ordinal)
            .ThenBy(t => t.FileName, StringComparer.Ordinal)
            .Select(t => t.Command!)
            .ToList();
    }

    [Fact]
    public void MultiResource_AllFamilies_MatchExpectedVerbBasedOrder()
    {
        foreach (var (familyName, tools) in MultiResourceFamilies)
        {
            var expected = GetExpectedMultiResourceOrder(tools);
            var actual = ToolOrderingPolicy.OrderForMultiResource(tools)
                .Select(t => t.Command!)
                .ToList();

            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }
        }
    }

    [Theory]
    [InlineData("compute-multi")]
    [InlineData("storage-multi")]
    [InlineData("network-multi")]
    public void MultiResource_PerFamily_IsStableAcrossInvocations(string familyName)
    {
        var tools = MultiResourceFamilies[familyName];

        var run1 = ToolOrderingPolicy.OrderForMultiResource(tools)
            .Select(t => t.Command!).ToList();
        var run2 = ToolOrderingPolicy.OrderForMultiResource(tools)
            .Select(t => t.Command!).ToList();

        Assert.Equal(run1, run2);
    }

    [Fact]
    public void MultiResource_ComputeFamily_VerifyExactOrder()
    {
        var tools = MultiResourceFamilies["compute-multi"];
        var ordered = ToolOrderingPolicy.OrderForMultiResource(tools)
            .Select(t => t.Command!)
            .ToList();

        // Expected: verbs sorted alphabetically -> create, create, delete, delete, list, list, start, update
        // Within same verb, tie-break by FileName (Ordinal)
        var expected = new List<string>
        {
            "compute disk create",   // create + disk-create.md
            "compute vm create",     // create + vm-create.md
            "compute disk delete",   // delete + disk-delete.md
            "compute vm delete",     // delete + vm-delete.md
            "compute disk list",     // list + disk-list.md
            "compute vm list",       // list + vm-list.md
            "compute vm start",      // start
            "compute vmss update",   // update
        };

        Assert.Equal(expected, ordered);
    }

    [Fact]
    public void MultiResource_StorageFamily_VerifyExactOrder()
    {
        var tools = MultiResourceFamilies["storage-multi"];
        var ordered = ToolOrderingPolicy.OrderForMultiResource(tools)
            .Select(t => t.Command!)
            .ToList();

        // Compute expected using the same algorithm
        var computedExpected = GetExpectedMultiResourceOrder(tools);
        Assert.Equal(computedExpected, ordered);
    }

    [Fact]
    public void MultiResource_ShuffledInput_ProducesSameOutput()
    {
        var tools = MultiResourceFamilies["network-multi"].ToList();
        var shuffled = new List<ToolContent>
        {
            tools[4], tools[1], tools[6], tools[0], tools[3], tools[5], tools[2]
        };

        var fromOriginal = ToolOrderingPolicy.OrderForMultiResource(tools)
            .Select(t => t.Command!).ToList();
        var fromShuffled = ToolOrderingPolicy.OrderForMultiResource(shuffled)
            .Select(t => t.Command!).ToList();

        Assert.Equal(fromOriginal, fromShuffled);
    }

    [Fact]
    public void MultiResource_EachFamilyHasAtLeast4Tools()
    {
        foreach (var (familyName, tools) in MultiResourceFamilies)
        {
            Assert.True(tools.Count >= 4,
                $"Family '{familyName}' has {tools.Count} tools, expected at least 4");
        }
    }

    [Fact]
    public void MultiResource_VerbExtraction_MatchesExpected()
    {
        var verbExpectations = new Dictionary<string, string>
        {
            ["compute vm create"] = "create",
            ["compute vm delete"] = "delete",
            ["compute vm list"] = "list",
            ["compute vm start"] = "start",
            ["compute disk create"] = "create",
            ["storage account create"] = "create",
            ["storage blob upload"] = "upload",
            ["network nsg show"] = "show",
            ["compute vmss update"] = "update",
        };

        foreach (var (command, expectedVerb) in verbExpectations)
        {
            Assert.Equal(expectedVerb, ToolOrderingPolicy.ExtractActionVerb(command));
        }
    }

    #endregion

    #region Helpers

    private static ToolContent MakeSingle(string toolName, string fileName, string familyName)
    {
        return new ToolContent
        {
            ToolName = toolName,
            FileName = fileName,
            FamilyName = familyName,
            Content = $"## {toolName}\n\nDocumentation content.",
            Command = toolName,
            ResourceType = "single",
        };
    }

    private static ToolContent MakeMulti(string toolName, string command, string fileName, string familyName)
    {
        return new ToolContent
        {
            ToolName = toolName,
            FileName = fileName,
            FamilyName = familyName,
            Content = $"## {toolName}\n\nDocumentation content.",
            Command = command,
            ResourceType = "multi",
        };
    }

    #endregion
}
