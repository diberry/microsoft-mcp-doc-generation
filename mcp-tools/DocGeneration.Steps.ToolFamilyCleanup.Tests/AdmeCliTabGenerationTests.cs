// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Shared;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Regression coverage for the ADME (Azure Data Manager for Energy) CLI tab
/// generation bug: the namespace allowlist/configuration used to gate CLI tab
/// content generation did not admit "adme", so a beta.44 generation run produced
/// 8 tool H2 sections with 0 Azure CLI / Azure MCP Server tab groups
/// (hand-repaired in MicrosoftDocs/azure-dev-docs-pr#9827, commit 3b43c0f2).
///
/// This test proves, using the real committed beta.44 CLI metadata fixture and
/// the real committed namespace-allowlist configuration, that:
///   1. "adme" is present in the namespace allowlist (config/namespace-mapping.json),
///      matching the mcpServerName to fileName pairing in brand-to-server-mapping.json.
///   2. The CLI tab config gate (CliTabConfig) admits "adme".
///   3. Wrapping a synthetic ADME family article (one H2 per beta.44 ADME tool)
///      with CliTabWrapper.ApplyTabsToFamilyArticle emits exactly 8 Azure CLI
///      tabs and 8 Azure MCP Server tabs - one well-formed pair per tool - with
///      no malformed/unpaired tab groups.
/// </summary>
public class AdmeCliTabGenerationTests
{
    private const string McpTabMarker = "#### [MCP Server](#tab/mcp-server)";
    private const string CliTabMarker = "#### [Azure MCP CLI](#tab/azure-mcp-cli)";

    private static string RepoRoot => _repoRoot.Value;
    private static readonly Lazy<string> _repoRoot = new(FindRepoRoot);

    /// <summary>
    /// Walks up from the test assembly directory using <see cref="DirectoryInfo.Parent"/>
    /// (rather than Path.Combine + GetFullPath, which mishandles ".." segments once the
    /// runtime reports an extended-length "\\?\" prefixed base directory for long paths,
    /// e.g. under a deeply nested isolated worktree clone).
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "mcp-doc-generation.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not find repo root (mcp-doc-generation.sln)");
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void NamespaceMappingAllowlist_ContainsAdme_MatchingBrandMapping()
    {
        var brandMappingPath = Path.Combine(RepoRoot, "mcp-tools", "data", "brand-to-server-mapping.json");
        Assert.True(File.Exists(brandMappingPath), $"brand-to-server-mapping.json not found: {brandMappingPath}");

        using var brandDoc = JsonDocument.Parse(File.ReadAllText(brandMappingPath));
        var admeEntry = brandDoc.RootElement.EnumerateArray()
            .FirstOrDefault(e => string.Equals(
                e.TryGetProperty("mcpServerName", out var name) ? name.GetString() : null,
                "adme",
                StringComparison.OrdinalIgnoreCase));

        Assert.True(admeEntry.ValueKind != JsonValueKind.Undefined,
            "brand-to-server-mapping.json must contain an 'adme' entry.");

        var expectedFileName = admeEntry.GetProperty("fileName").GetString();
        Assert.False(string.IsNullOrWhiteSpace(expectedFileName));

        var namespaceMappingPath = Path.Combine(RepoRoot, "config", "namespace-mapping.json");
        Assert.True(File.Exists(namespaceMappingPath), $"namespace-mapping.json not found: {namespaceMappingPath}");

        using var mappingDoc = JsonDocument.Parse(File.ReadAllText(namespaceMappingPath));
        Assert.True(mappingDoc.RootElement.TryGetProperty("adme", out var mappedFile),
            "config/namespace-mapping.json is the namespace allowlist/configuration gate and must contain an 'adme' entry " +
            "(it was previously absent, causing 0 CLI/MCP tab groups to be generated for the ADME article).");

        Assert.Equal($"{expectedFileName}.md", mappedFile.GetString());
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void CliTabConfig_ForNamespacesIncludingAdme_AllowsAdme()
    {
        var config = CliTabConfig.ForNamespaces("adme", "storage", "compute");

        Assert.True(config.IsNamespaceAllowed("adme"));
    }

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void ApplyTabsToFamilyArticle_Beta44AdmeTools_RendersExactly8CliAnd8McpTabs()
    {
        var admeCommands = LoadBeta44AdmeCommands();
        Assert.Equal(8, admeCommands.Count);

        var familyArticle = BuildSyntheticFamilyArticle(admeCommands);
        var cliContentByCommand = admeCommands.ToDictionary(
            command => command,
            command => $"```azurecli\naz {command}\n```",
            StringComparer.OrdinalIgnoreCase);

        var result = Shared.CliTabWrapper.ApplyTabsToFamilyArticle(familyArticle, cliContentByCommand);

        var mcpTabCount = CountOccurrences(result, McpTabMarker);
        var cliTabCount = CountOccurrences(result, CliTabMarker);

        Assert.Equal(8, mcpTabCount);
        Assert.Equal(8, cliTabCount);

        // No malformed groups: every CLI tab must precede its paired MCP tab,
        // and the counts must match the expected number of tool sections.
        AssertNoMalformedTabGroups(result, expectedPairs: 8);

        foreach (var command in admeCommands)
        {
            Assert.Contains($"az {command}", result);
        }
    }

    private static List<string> LoadBeta44AdmeCommands()
    {
        var cliOutputPath = Path.Combine(
            RepoRoot,
            "mcp-cli-metadata",
            "3.0.0-beta.44+32200bb8396d0f7dcbebc7f6d96a38fc147881d1",
            "cli-output.json");
        Assert.True(File.Exists(cliOutputPath), $"beta.44 cli-output.json fixture not found: {cliOutputPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(cliOutputPath));
        return document.RootElement.GetProperty("results")
            .EnumerateArray()
            .Select(tool => tool.GetProperty("command").GetString() ?? string.Empty)
            .Where(command => command.Equals("adme", StringComparison.OrdinalIgnoreCase)
                || command.StartsWith("adme ", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(command => command, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildSyntheticFamilyArticle(IReadOnlyList<string> commands)
    {
        var header = string.Join("\n", new[]
        {
            "---",
            "ms.topic: include",
            "---",
            "",
            "# Azure Data Manager for Energy tools",
            "",
        });

        var sections = commands.Select((command, index) => string.Join("\n", new[]
        {
            $"## Tool {index + 1}: {command}",
            $"<!-- @mcpcli {command} -->",
            "",
            $"Uses the `{command}` MCP tool to interact with ADME/OSDU.",
            "",
            "| Parameter | Required or optional | Description |",
            "|-----------|---------------------|-------------|",
            "| **Endpoint** | Required | ADME endpoint URL |",
            "",
            "---",
        }));

        return header + "\n\n" + string.Join("\n\n", sections);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    /// <summary>
    /// Verifies each CLI/MCP tab pair is well-formed: the CLI tab marker appears
    /// before its paired MCP tab marker, and the number of tab markers equals the
    /// expected pair count on both sides - i.e. no orphaned or duplicated tab groups.
    /// </summary>
    private static void AssertNoMalformedTabGroups(string content, int expectedPairs)
    {
        var lines = content.Split('\n');
        var cliIndexes = new List<int>();
        var mcpIndexes = new List<int>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(CliTabMarker, StringComparison.Ordinal)) cliIndexes.Add(i);
            if (lines[i].Contains(McpTabMarker, StringComparison.Ordinal)) mcpIndexes.Add(i);
        }

        Assert.Equal(expectedPairs, cliIndexes.Count);
        Assert.Equal(expectedPairs, mcpIndexes.Count);

        for (var pair = 0; pair < expectedPairs; pair++)
        {
            Assert.True(cliIndexes[pair] < mcpIndexes[pair],
                $"Tab group {pair}: expected the Azure CLI tab to precede the MCP Server tab.");
        }
    }
}
