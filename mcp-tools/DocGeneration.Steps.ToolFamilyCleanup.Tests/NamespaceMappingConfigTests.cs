// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Regression coverage for config/namespace-mapping.json, the namespace-to-file
/// configuration consumed by ToolFamilyPostAssemblyValidator.GetNamespaceFilePrefixesAsync
/// during post-assembly validation. This file was missing an "adme" entry even though
/// mcp-tools/data/brand-to-server-mapping.json already maps adme -> azure-data-manager-for-energy
/// (added in the beta.44 bump, commit 0d092d2). This complements the ToolFamilyCleanupStep
/// fail-loud fix (#829) by ensuring ADME is also correctly recognized by post-assembly
/// file-prefix validation, not only by CLI tab wrapping.
/// </summary>
public class NamespaceMappingConfigTests
{
    private static string RepoRoot => FindRepoRoot();

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void NamespaceMapping_ContainsAdme_MatchingBrandToServerMapping()
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
            "config/namespace-mapping.json must contain an 'adme' entry so post-assembly " +
            "file-prefix validation (ToolFamilyPostAssemblyValidator) correctly recognizes ADME.");

        Assert.Equal($"{expectedFileName}.md", mappedFile.GetString());
    }

    /// <summary>
    /// Walks up from the test assembly directory using DirectoryInfo.Parent (rather than
    /// Path.Combine + GetFullPath, which mishandles ".." segments once the runtime reports
    /// an extended-length "\\?\" prefixed base directory for long paths, e.g. under a
    /// deeply nested isolated worktree clone).
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
}
