// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Shared;

/// <summary>
/// Maps CLI JSON output into <see cref="CliToolInfo"/> dictionaries.
/// </summary>
public static class CliJsonMapper
{
    /// <summary>
    /// Maps cli-output.json content to dictionary keyed by normalized command string.
    /// </summary>
    public static IReadOnlyDictionary<string, CliToolInfo> MapFromCliOutput(string json)
    {
        var root = ParseRoot(json);
        var results = GetResultsArray(root);
        return MapResults(results, enriched: false);
    }

    /// <summary>
    /// Maps cli-output-enriched.json, merging defaults/valuePlaceholder/allowedValues from enrichment.
    /// </summary>
    public static IReadOnlyDictionary<string, CliToolInfo> MapFromEnrichedCliOutput(string json)
    {
        var root = ParseRoot(json);
        var results = GetResultsArray(root);
        return MapResults(results, enriched: true);
    }

    /// <summary>
    /// Normalizes a command string for use as dictionary key.
    /// Trims, collapses whitespace, lowercases.
    /// </summary>
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);

    internal static string NormalizeCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return "";

        var trimmed = command.Trim();
        var collapsed = WhitespacePattern.Replace(trimmed, " ");
        return collapsed.ToLowerInvariant();
    }

    private static JsonElement ParseRoot(string json)
    {
        try
        {
            var sanitized = JsonControlCharacterSanitizer.StripInvalidControlCharacters(json);
            using var doc = JsonDocument.Parse(sanitized);
            // Clone so it survives disposal
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse CLI JSON: {ex.Message}", ex);
        }
    }

    private static JsonElement GetResultsArray(JsonElement root)
    {
        if (!root.TryGetProperty("results", out var results))
            throw new InvalidOperationException("CLI JSON is missing required 'results' property.");

        if (results.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("CLI JSON 'results' property must be an array.");

        return results;
    }

    private static IReadOnlyDictionary<string, CliToolInfo> MapResults(JsonElement results, bool enriched)
    {
        var dict = new Dictionary<string, CliToolInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in results.EnumerateArray())
        {
            if (!item.TryGetProperty("command", out var commandProp))
            {
                Console.WriteLine("Warning: Skipping result with no 'command' property.");
                continue;
            }

            var rawCommand = commandProp.GetString();
            if (string.IsNullOrWhiteSpace(rawCommand))
            {
                Console.WriteLine("Warning: Skipping result with empty command.");
                continue;
            }

            var key = NormalizeCommand(rawCommand);
            var description = GetOptionalString(item, "description");
            var switches = MapSwitches(item);
            var (isDestructive, isReadOnly) = MapMetadata(item);

            bool? enrichmentMatched = null;
            if (enriched && item.TryGetProperty("enrichment", out var enrichment))
            {
                enrichmentMatched = GetOptionalBool(enrichment, "matched");
                switches = MergeEnrichment(switches, enrichment);
            }

            var tool = new CliToolInfo(
                Command: rawCommand.Trim(),
                Description: description,
                Switches: switches,
                IsDestructive: isDestructive,
                IsReadOnly: isReadOnly,
                EnrichmentMatched: enrichmentMatched);

            if (dict.ContainsKey(key))
                Console.WriteLine($"Warning: Duplicate command '{rawCommand}' — last one wins.");

            dict[key] = tool;
        }

        return dict;
    }

    private static List<CliSwitch> MapSwitches(JsonElement item)
    {
        var switches = new List<CliSwitch>();

        if (!item.TryGetProperty("option", out var options) || options.ValueKind != JsonValueKind.Array)
            return switches;

        foreach (var opt in options.EnumerateArray())
        {
            if (!opt.TryGetProperty("name", out var nameProp))
            {
                Console.WriteLine("Warning: Skipping option with no 'name' property.");
                continue;
            }

            var name = nameProp.GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                Console.WriteLine("Warning: Skipping option with empty name.");
                continue;
            }

            switches.Add(new CliSwitch(
                Name: name,
                Description: GetOptionalString(opt, "description"),
                Type: GetOptionalString(opt, "type", "string"),
                IsRequired: GetOptionalBool(opt, "required")));
        }

        return switches;
    }

    private static (bool isDestructive, bool isReadOnly) MapMetadata(JsonElement item)
    {
        bool isDestructive = false;
        bool isReadOnly = false;

        if (item.TryGetProperty("metadata", out var metadata))
        {
            if (metadata.TryGetProperty("destructive", out var destructive))
                isDestructive = GetOptionalBool(destructive, "value") ?? false;

            if (metadata.TryGetProperty("readOnly", out var readOnly))
                isReadOnly = GetOptionalBool(readOnly, "value") ?? false;
        }

        return (isDestructive, isReadOnly);
    }

    private static List<CliSwitch> MergeEnrichment(List<CliSwitch> switches, JsonElement enrichment)
    {
        if (!enrichment.TryGetProperty("parameterEnhancements", out var enhancements)
            || enhancements.ValueKind != JsonValueKind.Object)
            return switches;

        var result = new List<CliSwitch>(switches.Count);

        foreach (var sw in switches)
        {
            if (enhancements.TryGetProperty(sw.Name, out var enhancement))
            {
                var merged = sw with
                {
                    Default = GetOptionalString(enhancement, "default") is { Length: > 0 } d ? d : sw.Default,
                    ValuePlaceholder = GetOptionalString(enhancement, "valuePlaceholder") is { Length: > 0 } v ? v : sw.ValuePlaceholder,
                    AllowedValues = MapAllowedValues(enhancement) ?? sw.AllowedValues
                };
                result.Add(merged);
            }
            else
            {
                result.Add(sw);
            }
        }

        return result;
    }

    private static IReadOnlyList<string>? MapAllowedValues(JsonElement enhancement)
    {
        if (!enhancement.TryGetProperty("allowedValues", out var allowed) || allowed.ValueKind != JsonValueKind.Array)
            return null;

        var values = new List<string>();
        foreach (var v in allowed.EnumerateArray())
        {
            var s = v.GetString();
            if (s is not null)
                values.Add(s);
        }

        return values.Count > 0 ? values : null;
    }

    private static string GetOptionalString(JsonElement element, string property, string fallback = "")
    {
        if (element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString() ?? fallback;
        return fallback;
    }

    private static bool? GetOptionalBool(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
        }
        return null;
    }
}
