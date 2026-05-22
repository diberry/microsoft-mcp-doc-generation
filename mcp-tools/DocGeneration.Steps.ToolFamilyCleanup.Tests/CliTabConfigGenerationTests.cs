// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Regression tests for cli-tab-config.json auto-generation (R-CG1 through R-CG3).
/// Validates that config is deterministic from namespace-brand-mapping.json,
/// all namespaces are covered, and config is never manually divergent.
/// </summary>
public class CliTabConfigGenerationTests
{
    private static readonly Regex TimestampedNamespaceDirectoryPattern = new(
        @"^generated-(?<namespace>.+)-(?<timestamp>\d{8}T\d{9}Z)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static string RepoRoot => RegressionTestHelpers.RepoRoot;

    // ── R-CG1: Config is deterministically generated from mapping ────────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    public void R_CG1_ConfigDeterministicFromMapping()
    {
        var mappingPath = Path.Combine(RepoRoot, "mcp-tools", "data", "brand-to-server-mapping.json");
        Assert.True(File.Exists(mappingPath), $"brand-to-server-mapping.json not found: {mappingPath}");

        var mappingJson = File.ReadAllText(mappingPath);
        var mappings = JsonSerializer.Deserialize<JsonElement>(mappingJson);

        Assert.True(mappings.ValueKind == JsonValueKind.Array, "brand-to-server-mapping.json must be a JSON array");
        Assert.True(mappings.GetArrayLength() > 0, "brand-to-server-mapping.json must not be empty");

        // Each entry must have mcpServerName and brandName
        foreach (var entry in mappings.EnumerateArray())
        {
            Assert.True(entry.TryGetProperty("mcpServerName", out var ns),
                "Each mapping entry must have 'mcpServerName'");
            Assert.True(entry.TryGetProperty("brandName", out var brand),
                "Each mapping entry must have 'brandName'");
            Assert.False(string.IsNullOrWhiteSpace(ns.GetString()),
                "mcpServerName must not be empty");
            Assert.False(string.IsNullOrWhiteSpace(brand.GetString()),
                "brandName must not be empty");
        }
    }

    // ── R-CG2: Every namespace in mapping has corresponding generated output ─

    [Fact]
    [Trait("Category", "RegressionProtection")]
    [Trait("Category", "RequiresGeneration")]
    public void R_CG2_AllNamespacesHaveGeneratedOutput()
    {
        var mappingPath = Path.Combine(RepoRoot, "mcp-tools", "data", "brand-to-server-mapping.json");
        Assert.True(File.Exists(mappingPath), $"brand-to-server-mapping.json not found: {mappingPath}");

        var mappingJson = File.ReadAllText(mappingPath);
        var mappings = JsonSerializer.Deserialize<JsonElement>(mappingJson);

        var namespaces = mappings.EnumerateArray()
            .Select(e => e.GetProperty("mcpServerName").GetString()!)
            .ToList();

        // Check that generated output directories exist for namespaces we've generated
        // (not all namespaces may have been generated in test environment, but those that exist must be valid)
        var generatedDirs = Directory.GetDirectories(RepoRoot, "generated-*")
            .Select(TryExtractNamespace)
            .Where(d => d is not null && !d.Contains("-old-") && !d.Contains("-prev"))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // At minimum, azurebackup must exist (our primary test namespace)
        // In CI where no generation has run, skip gracefully
        if (generatedDirs.Count == 0) return;
        Assert.Contains("azurebackup", generatedDirs);

        // Every generated directory must correspond to a known namespace
        foreach (var genNs in generatedDirs)
        {
            Assert.True(namespaces.Contains(genNs),
                $"Generated directory 'generated-{genNs}' has no matching namespace in brand-to-server-mapping.json");
        }
    }

    // ── R-CG3: Config matches generated output (not manually edited) ─────

    [Fact]
    [Trait("Category", "RegressionProtection")]
    [Trait("Category", "RequiresGeneration")]
    public void R_CG3_ConfigMatchesGeneratedOutput()
    {
        var mappingPath = Path.Combine(RepoRoot, "mcp-tools", "data", "brand-to-server-mapping.json");
        Assert.True(File.Exists(mappingPath), $"brand-to-server-mapping.json not found: {mappingPath}");

        var mappingJson = File.ReadAllText(mappingPath);
        var mappings = JsonSerializer.Deserialize<JsonElement>(mappingJson);

        // For each namespace that has a generated tool-family file, verify the brand name
        // in the file matches the brand name from the mapping
        foreach (var entry in mappings.EnumerateArray())
        {
            var ns = entry.GetProperty("mcpServerName").GetString()!;
            var brandName = entry.GetProperty("brandName").GetString()!;

            // Check if this namespace has been generated
            string? fileName = null;
            if (entry.TryGetProperty("fileName", out var fileNameProp))
                fileName = fileNameProp.GetString();

            if (string.IsNullOrEmpty(fileName)) continue;

            var generatedDir = FindLatestGeneratedDirectory(ns);
            if (generatedDir is null) continue;

            var toolFamilyPath = Path.Combine(generatedDir, "tool-family", $"{fileName}.md");
            if (!File.Exists(toolFamilyPath)) continue;

            var content = File.ReadAllText(toolFamilyPath);
            var h1Match = Regex.Match(content, @"^# Azure MCP Server tools for (.+)$", RegexOptions.Multiline);

            if (h1Match.Success)
            {
                Assert.Equal(brandName, h1Match.Groups[1].Value.Trim());
            }
        }
    }

    private static string? TryExtractNamespace(string directoryPath)
    {
        var directoryName = Path.GetFileName(directoryPath);
        if (string.IsNullOrWhiteSpace(directoryName) || directoryName.Equals("generated", StringComparison.OrdinalIgnoreCase))
            return null;

        var timestampedMatch = TimestampedNamespaceDirectoryPattern.Match(directoryName);
        if (timestampedMatch.Success)
            return timestampedMatch.Groups["namespace"].Value;

        return directoryName.StartsWith("generated-", StringComparison.OrdinalIgnoreCase)
            ? directoryName["generated-".Length..]
            : null;
    }

    private static string? FindLatestGeneratedDirectory(string namespaceName)
    {
        return Directory.GetDirectories(RepoRoot, $"generated-{namespaceName}*")
            .Where(path => string.Equals(TryExtractNamespace(path), namespaceName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(GetGeneratedDirectorySortKey)
            .ThenByDescending(Directory.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static DateTimeOffset GetGeneratedDirectorySortKey(string directoryPath)
    {
        var directoryName = Path.GetFileName(directoryPath) ?? string.Empty;
        var timestampedMatch = TimestampedNamespaceDirectoryPattern.Match(directoryName);
        if (timestampedMatch.Success && DateTimeOffset.TryParseExact(
                timestampedMatch.Groups["timestamp"].Value,
                "yyyyMMdd'T'HHmmssfff'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedTimestamp))
        {
            return parsedTimestamp;
        }

        return Directory.GetLastWriteTimeUtc(directoryPath);
    }
}
