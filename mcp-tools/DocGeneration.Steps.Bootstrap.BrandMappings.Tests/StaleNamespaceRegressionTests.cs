// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Xunit;

namespace BrandMapperValidator.Tests;

/// <summary>
/// Regression guards: stale namespaces that were removed from beta.31 CLI output
/// must never reappear in brand-to-server-mapping.json. If they do, the drift
/// detection in BootstrapStep will hard-error and block ALL namespace generation.
/// </summary>
public class StaleNamespaceRegressionTests
{
    /// <summary>
    /// Resolves the real brand-to-server-mapping.json in the source tree.
    /// </summary>
    private static string GetRealBrandMappingPath()
    {
        // Walk up from bin/Release/net10.0 to mcp-tools/data/
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "mcp-tools", "data", "brand-to-server-mapping.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException(
            "Could not locate brand-to-server-mapping.json from test bin directory.");
    }

    private static HashSet<string> LoadMcpServerNames()
    {
        var path = GetRealBrandMappingPath();
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("mcpServerName").GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("foundry")]
    [InlineData("get")]
    public void BrandMapping_DoesNotContain_StaleNamespace(string staleNamespace)
    {
        var serverNames = LoadMcpServerNames();

        Assert.DoesNotContain(staleNamespace, serverNames);
    }

    [Fact]
    public void BrandMapping_Contains_FoundryExtensions_Replacement()
    {
        var serverNames = LoadMcpServerNames();

        // foundryextensions replaced foundry in beta.31
        Assert.Contains("foundryextensions", serverNames);
    }
}
