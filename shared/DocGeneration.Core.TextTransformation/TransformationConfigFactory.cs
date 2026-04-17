// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using Azure.Mcp.TextTransformation.Models;

namespace Azure.Mcp.TextTransformation;

/// <summary>
/// Builds a <see cref="TransformationConfig"/> from the legacy NaturalLanguage JSON data files
/// (nl-parameters.json, static-text-replacement.json, nl-parameter-identifiers.json).
/// </summary>
public static class TransformationConfigFactory
{
    /// <summary>
    /// Creates a TransformationConfig from legacy required-files list.
    /// Reads nl-parameters.json and static-text-replacement.json from the list,
    /// auto-discovers nl-parameter-identifiers.json from the nl-parameters.json directory,
    /// and populates the config with combined mappings (first-entry-wins dedup).
    /// </summary>
    public static TransformationConfig CreateFromLegacyFiles(List<string> requiredFiles)
    {
        if (requiredFiles == null || requiredFiles.Count == 0)
            throw new ArgumentException("RequiredFiles list is null or empty.", nameof(requiredFiles));

        string? nlParametersPath = null;
        string? textReplacerParametersPath = null;

        foreach (var file in requiredFiles)
        {
            if (file.IndexOf("nl-parameters.json", StringComparison.OrdinalIgnoreCase) >= 0)
                nlParametersPath = file;
            if (file.IndexOf("static-text-replacement.json", StringComparison.OrdinalIgnoreCase) >= 0)
                textReplacerParametersPath = file;
        }

        // Load raw MappedParameter arrays in the legacy format {Parameter, NaturalLanguage}
        var nlParams = LoadLegacyMappings(nlParametersPath);
        var staticReplacements = LoadLegacyMappings(textReplacerParametersPath);

        // Combined list: nl-parameters FIRST, then static-text-replacement (first-entry-wins dedup)
        var combined = new List<LegacyMapping>();
        combined.AddRange(nlParams);
        combined.AddRange(staticReplacements);

        // Deduplicate case-insensitively, first entry wins
        var deduped = combined
            .GroupBy(m => m.Parameter, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        // Auto-discover nl-parameter-identifiers.json from the same directory as nl-parameters.json
        var identifiers = new List<LegacyMapping>();
        if (!string.IsNullOrEmpty(nlParametersPath))
        {
            var dir = Path.GetDirectoryName(nlParametersPath);
            if (dir != null)
            {
                var identifiersPath = Path.Combine(dir, "nl-parameter-identifiers.json");
                identifiers = LoadLegacyMappings(identifiersPath);
            }
        }

        var config = new TransformationConfig();

        // Parameters.Identifiers ← nl-parameter-identifiers.json
        config.Parameters.Identifiers = identifiers
            .Where(m => !string.IsNullOrEmpty(m.Parameter))
            .Select(m => new ParameterMapping
            {
                Parameter = m.Parameter,
                Display = m.NaturalLanguage
            })
            .ToList();

        // Parameters.Mappings ← nl-parameters.json only (full-name lookup in NormalizeParameter)
        config.Parameters.Mappings = nlParams
            .Where(m => !string.IsNullOrEmpty(m.Parameter))
            .Select(m => new ParameterMapping
            {
                Parameter = m.Parameter,
                Display = m.NaturalLanguage
            })
            .ToList();

        // Lexicon.Abbreviations ← entire combined+deduped dict (used by ReplaceStaticText
        // AND per-word lookup in NormalizeParameter fallback)
        config.Lexicon.Abbreviations = new Dictionary<string, AbbreviationEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in deduped)
        {
            if (!string.IsNullOrEmpty(m.Parameter))
            {
                config.Lexicon.Abbreviations[m.Parameter] = new AbbreviationEntry
                {
                    Canonical = m.NaturalLanguage
                };
            }
        }

        // Lexicon.Acronyms ← hardcoded acronym table matching standard acronym transformations
        config.Lexicon.Acronyms = BuildLegacyAcronyms();

        return config;
    }

    private static List<LegacyMapping> LoadLegacyMappings(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return new List<LegacyMapping>();

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<LegacyMapping>>(json) ?? new List<LegacyMapping>();
    }

    private static Dictionary<string, AcronymEntry> BuildLegacyAcronyms()
    {
        // Must match the standard acronym transformations + IsAcronym exactly
        var acronyms = new Dictionary<string, AcronymEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = new() { Canonical = "ID" },
            ["ids"] = new() { Canonical = "IDs" },
            ["uri"] = new() { Canonical = "URI" },
            ["url"] = new() { Canonical = "URL" },
            ["urls"] = new() { Canonical = "URLs" },
            ["ai"] = new() { Canonical = "AI" },
            ["api"] = new() { Canonical = "API" },
            ["apis"] = new() { Canonical = "APIs" },
            ["cpu"] = new() { Canonical = "CPU" },
            ["gpu"] = new() { Canonical = "GPU" },
            ["ip"] = new() { Canonical = "IP" },
            ["sql"] = new() { Canonical = "SQL" },
            ["vm"] = new() { Canonical = "VM" },
            ["vms"] = new() { Canonical = "VMs" },
            ["dns"] = new() { Canonical = "DNS" },
            ["sku"] = new() { Canonical = "SKU" },
            ["skus"] = new() { Canonical = "SKUs" },
            ["tls"] = new() { Canonical = "TLS" },
            ["ssl"] = new() { Canonical = "SSL" },
            ["http"] = new() { Canonical = "HTTP" },
            ["https"] = new() { Canonical = "HTTPS" },
            ["json"] = new() { Canonical = "JSON" },
            ["xml"] = new() { Canonical = "XML" },
            ["yaml"] = new() { Canonical = "YAML" },
            ["oauth"] = new() { Canonical = "OAuth" },
            ["etag"] = new() { Canonical = "ETag" },
            ["cdn"] = new() { Canonical = "CDN" },
            ["rg"] = new() { Canonical = "Resource group" }
        };
        return acronyms;
    }

    /// <summary>
    /// Legacy DTO matching the {Parameter, NaturalLanguage} shape of the old JSON files.
    /// </summary>
    private sealed class LegacyMapping
    {
        public string Parameter { get; set; } = string.Empty;
        public string NaturalLanguage { get; set; } = string.Empty;
    }
}
