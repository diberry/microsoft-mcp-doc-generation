// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CSharpGenerator.Models;
using Shared;
using TemplateEngine;
using ToolFamilyCleanup.Services;
using static CSharpGenerator.Generators.FrontmatterUtility;
using System.Text;

namespace CSharpGenerator.Generators;

/// <summary>
/// Generates parameter files for MCP tools
/// </summary>
public class ParameterGenerator
{
    /// <summary>
    /// Generates parameter files for all tools
    /// </summary>
    public async Task GenerateParameterFilesAsync(
        TransformedData data, 
        string outputDir, 
        string templateFile)
    {
        try
        {
            Console.WriteLine($"Generating parameter files for {data.Tools.Count} tools...");
            
            // Load shared data files for filename generation
            var nameContext = await FileNameContext.CreateAsync();
            
            // Keep common parameter metadata for canonical descriptions, but include
            // every named parameter in generated tool output.
            var commonParameters = data.SourceDiscoveredCommonParams;
            var ignoredCommonParameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Build canonical description lookup for consistent parameter descriptions
            // GroupBy ensures no duplicate key exceptions if source has case-variant duplicates
            var canonicalDescriptions = commonParameters
                .Where(p => !string.IsNullOrEmpty(p.Name) && !string.IsNullOrEmpty(p.Description))
                .GroupBy(p => p.Name!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Description, StringComparer.OrdinalIgnoreCase);
            
            foreach (var tool in data.Tools)
            {
                if (string.IsNullOrEmpty(tool.Command))
                    continue;

                // Use shared deterministic filename builder
                var fileName = ToolFileNameBuilder.BuildParameterFileName(
                    tool.Command, nameContext);
                var manifestFileName = ToolFileNameBuilder.BuildParameterManifestFileName(
                    tool.Command, nameContext);
                var outputFile = Path.Combine(outputDir, fileName);
                var manifestOutputFile = Path.Combine(outputDir, manifestFileName);

                var allOptions = tool.Option ?? new List<Option>();
                
                var conditionalParameters = new HashSet<string>(
                    tool.ConditionalRequiredParameters ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase);

                var filteredOptions = allOptions
                    .Where(opt => ParameterFilterHelper.ShouldInclude(opt, ignoredCommonParameterNames))
                    .ToList();

                var parameterManifest = BuildParameterManifest(filteredOptions, conditionalParameters, canonicalDescriptions);

                var transformedOptions = filteredOptions
                    .Zip(parameterManifest, (opt, manifest) => new
                    {
                        name = manifest.Name,
                        // IMPORTANT: manifest.DisplayName comes from NormalizeParameter and preserves
                        // all words, including type qualifiers like "name" in "resource-group-name".
                        NL_Name = manifest.DisplayName,
                        type = opt.Type,
                        required = manifest.Required,
                        RequiredText = manifest.RequiredText,
                        description = manifest.Description
                    })
                    .ToList();

                var parameterData = new Dictionary<string, object>
                {
                    ["tool"] = tool,
                    ["command"] = tool.Command ?? "",
                    ["area"] = tool.Area ?? "",
                    ["option"] = (object?)transformedOptions ?? new List<object>(),
                    ["hasConditionalRequired"] = transformedOptions?.Any(o => o.RequiredText.EndsWith("*", StringComparison.Ordinal)) ?? false,
                    ["generateParameter"] = true,
                    ["generatedAt"] = data.GeneratedAt,
                    ["version"] = data.Version ?? "unknown",
                    ["parameterFileName"] = fileName
                };

                var templateResult = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, parameterData);
                var frontmatter = FrontmatterUtility.GenerateParameterFrontmatter(
                    tool.Command ?? "unknown",
                    data.Version,
                    fileName);
                var result = frontmatter + templateResult;
                await File.WriteAllTextAsync(outputFile, result, Encoding.UTF8);

                // Build v2 canonical manifest for the JSON file.
                // The legacy BuildParameterManifest result (parameterManifest) feeds the
                // Handlebars template above; the v2 overload is used for disk serialization.
                var rawInputs = filteredOptions
                    .Zip(parameterManifest, (opt, legacy) => new RawParameterInput(
                        legacy.Name,
                        legacy.DisplayName,
                        legacy.Required,
                        legacy.RequiredText,
                        legacy.IsConditionalRequired,
                        legacy.Description))
                    .ToList();

                var v2Manifest = BuildParameterManifest(
                    tool.Command!, tool.Area ?? "", data.Version ?? "unknown", rawInputs);

                await File.WriteAllTextAsync(
                    manifestOutputFile,
                    JsonSerializer.Serialize(v2Manifest, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    }),
                    Encoding.UTF8);
                tool.HasParameters = true;
            }
            
            Console.WriteLine($"✓ Generated {data.Tools.Count} parameter files");
        }
        catch (Exception ex)
        {
            LogFileHelper.WriteDebug($"Error generating parameter files: {ex.Message}");
            LogFileHelper.WriteDebug(ex.StackTrace ?? "No stack trace");
            throw;
        }
    }

    internal static List<ParameterManifestEntry> BuildParameterManifest(
        IEnumerable<Option> options,
        HashSet<string> conditionalParameters,
        Dictionary<string, string>? canonicalDescriptions = null)
    {
        return options
            .Select(opt =>
            {
                var parameterName = opt.Name ?? string.Empty;
                
                // Use canonical description for common/global parameters when available
                var rawDescription = canonicalDescriptions != null 
                    && canonicalDescriptions.TryGetValue(parameterName, out var canonical)
                    ? canonical
                    : opt.Description ?? string.Empty;
                
                return new ParameterManifestEntry
                {
                    Name = parameterName,
                    DisplayName = Config.TextNormalizer.NormalizeParameter(parameterName),
                    Required = opt.Required,
                    RequiredText = BuildRequiredText(opt.Required, parameterName, conditionalParameters),
                    IsConditionalRequired = conditionalParameters.Contains(parameterName),
                    Description = ParameterDescriptionBackticker.Apply(
                        Config.TextNormalizer.WrapExampleValues(
                            Config.TextNormalizer.EnsureEndsPeriod(
                                Config.TextNormalizer.ReplaceStaticText(rawDescription))))
                };
            })
            .ToList();
    }

    /// <summary>
    /// Builds the "Required or optional" column text for a parameter.
    /// 
    /// Parameters can be both optional and conditional. The requirement level is
    /// a combination of the base level (Required/Optional) and a conditional modifier (*).
    /// Possible outputs:
    ///   - "Required"  — always required.
    ///   - "Optional"  — never required.
    ///   - "Required*" — required, and part of a conditional group.
    ///   - "Optional*" — optional by default, but conditionally required depending on
    ///                    how other parameters in the group are used.
    /// 
    /// The asterisk (*) pairs with a footnote in the rendered parameter table that
    /// explains the conditional relationship (e.g., "At least one of the parameters
    /// marked with * is required").
    /// </summary>
    internal static string BuildRequiredText(bool required, string parameterName, HashSet<string> conditionalParameters)
    {
        var baseText = required ? "Required" : "Optional";
        if (conditionalParameters.Contains(parameterName))
        {
            return baseText + "*";
        }

        return baseText;
    }

    internal sealed class ParameterManifestEntry
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool Required { get; set; }
        public string RequiredText { get; set; } = string.Empty;
        public bool IsConditionalRequired { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Builds a v2 canonical parameter manifest from raw parameter inputs.
    /// Derives aliases deterministically and applies collision elimination per AD-030 §1.3 / C3.
    /// </summary>
    public static CanonicalParameterManifest BuildParameterManifest(
        string toolCommand,
        string toolNamespace,
        string azureMcpBuild,
        IReadOnlyList<RawParameterInput> parameters)
    {
        var generatedAt = DateTime.UtcNow.ToString("O");

        // Step 1: Build entries with initial alias derivation
        var entries = new List<(string Canonical, string DisplayName, List<string> DisplayAliases, List<string> PlaceholderAliases, RawParameterInput Raw)>();
        foreach (var param in parameters)
        {
            var canonical = CanonicalParameterNormalizer.Normalize(param.Name);
            var displayAliases = CanonicalAliasDeriver.DeriveDisplayAliases(canonical, param.DisplayName).ToList();
            var placeholderAliases = CanonicalAliasDeriver.DerivePlaceholderAliases(canonical, param.DisplayName).ToList();
            entries.Add((canonical, param.DisplayName, displayAliases, placeholderAliases, param));
        }

        // Step 2: Collision elimination per C3
        var allCanonicals = entries.Select(e => e.Canonical).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Find display alias collisions (same alias claimed by multiple params)
        var displayAliasCounts = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < entries.Count; i++)
        {
            foreach (var alias in entries[i].DisplayAliases)
            {
                if (!displayAliasCounts.TryGetValue(alias, out var owners))
                {
                    owners = new List<int>();
                    displayAliasCounts[alias] = owners;
                }
                owners.Add(i);
            }
        }

        // Remove colliding display aliases from ALL owners
        foreach (var (alias, owners) in displayAliasCounts)
        {
            if (owners.Count > 1)
            {
                foreach (var idx in owners)
                {
                    entries[idx].DisplayAliases.Remove(alias);
                }
            }
        }

        // Remove display aliases that shadow another parameter's canonical name
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].DisplayAliases.RemoveAll(alias =>
                allCanonicals.Contains(alias) &&
                !string.Equals(alias, entries[i].Canonical, StringComparison.OrdinalIgnoreCase));
        }

        // Find placeholder alias collisions
        var placeholderCounts = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < entries.Count; i++)
        {
            foreach (var alias in entries[i].PlaceholderAliases)
            {
                if (!placeholderCounts.TryGetValue(alias, out var owners))
                {
                    owners = new List<int>();
                    placeholderCounts[alias] = owners;
                }
                owners.Add(i);
            }
        }

        // Remove colliding placeholder aliases from ALL owners
        foreach (var (alias, owners) in placeholderCounts)
        {
            if (owners.Count > 1)
            {
                foreach (var idx in owners)
                {
                    entries[idx].PlaceholderAliases.Remove(alias);
                }
            }
        }

        // Remove placeholder aliases that shadow another parameter's canonical name
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].PlaceholderAliases.RemoveAll(alias =>
                allCanonicals.Contains(alias) &&
                !string.Equals(alias, entries[i].Canonical, StringComparison.OrdinalIgnoreCase));
        }

        // Step 3: Build final entries
        var finalParams = entries.Select(e => new CanonicalParameterEntry(
            e.Canonical,
            e.DisplayName,
            e.DisplayAliases.Distinct(StringComparer.Ordinal).ToArray(),
            e.PlaceholderAliases.Distinct(StringComparer.Ordinal).ToArray(),
            e.Raw.Required,
            e.Raw.RequiredText,
            e.Raw.IsConditionalRequired,
            e.Raw.Description)).ToArray();

        // Build placeholder index
        var placeholderIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in finalParams)
        {
            foreach (var alias in p.PlaceholderAliases)
            {
                placeholderIndex.TryAdd(alias, p.CanonicalName);
            }
        }

        return new CanonicalParameterManifest(
            "2.0",
            toolCommand,
            toolNamespace,
            new ManifestSourceIdentity(azureMcpBuild, generatedAt),
            finalParams,
            placeholderIndex);
    }
}
