// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shared;

/// <summary>
/// Immutable context object holding the shared data files needed for filename generation.
/// Avoids repeating three dictionary parameters across every method call.
/// </summary>
public sealed class FileNameContext
{
    public Dictionary<string, BrandMapping> BrandMappings { get; }
    public Dictionary<string, string> CompoundWords { get; }
    public HashSet<string> StopWords { get; }

    public FileNameContext(
        Dictionary<string, BrandMapping> brandMappings,
        Dictionary<string, string> compoundWords,
        HashSet<string> stopWords)
    {
        BrandMappings = brandMappings;
        CompoundWords = compoundWords;
        StopWords = stopWords;
    }

    /// <summary>
    /// Loads all data files and creates a context. Caches via DataFileLoader.
    /// </summary>
    public static async Task<FileNameContext> CreateAsync()
    {
        var brandMappings = await DataFileLoader.LoadBrandMappingsAsync();
        var compoundWords = await DataFileLoader.LoadCompoundWordsAsync();
        var stopWords = await DataFileLoader.LoadStopWordsAsync();
        return new FileNameContext(brandMappings, compoundWords, stopWords);
    }
}

/// <summary>
/// Deterministic, idempotent filename builder for tool documentation files.
/// All generators must use this to derive filenames from CLI command names,
/// ensuring consistent naming across annotations, parameters, example prompts,
/// raw tools, and composed tools.
///
/// Filename formula:
///   {brandPrefix}-{cleanedRemainingParts}-{suffix}.md
///
/// Where:
///   brandPrefix = brand mapping FileName (e.g., "azure-kubernetes-service"),
///                 or compound-word-expanded area with "azure-" prefix
///   cleanedRemainingParts = remaining command parts with compound words expanded
///                           and stop words removed
///   suffix = content type (e.g., "parameters", "annotations", "example-prompts")
///            or empty for raw/composed tool files
/// </summary>
public static class ToolFileNameBuilder
{
    // ── Core builder (context overload) ─────────────────────────────────

    /// <summary>
    /// Builds the canonical base filename for a tool (without suffix or extension).
    /// This is the single source of truth for tool file naming.
    /// </summary>
    /// <param name="command">The CLI command (e.g., "aks nodepool get")</param>
    /// <param name="ctx">Shared data file context</param>
    /// <returns>Base filename like "azure-kubernetes-service-node-pool-get"</returns>
    public static string BuildBaseFileName(string command, FileNameContext ctx)
        => BuildBaseFileName(command, ctx.BrandMappings, ctx.CompoundWords, ctx.StopWords);

    // ── Core builder (explicit params overload) ─────────────────────────

    /// <summary>
    /// Builds the canonical base filename for a tool (without suffix or extension).
    /// This is the single source of truth for tool file naming.
    /// </summary>
    public static string BuildBaseFileName(
        string command,
        Dictionary<string, BrandMapping> brandMappings,
        Dictionary<string, string> compoundWords,
        HashSet<string> stopWords)
    {
        if (string.IsNullOrWhiteSpace(command))
            return "unknown";

        var commandParts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (commandParts.Length == 0)
            return "unknown";

        var area = commandParts[0];

        // Step 1: Resolve brand prefix from brand mapping, compound words, or raw area name
        string brandPrefix;
        if (brandMappings.TryGetValue(area, out var mapping) && !string.IsNullOrEmpty(mapping.FileName))
        {
            brandPrefix = mapping.FileName;
        }
        else
        {
            var areaLower = area.ToLowerInvariant();
            brandPrefix = compoundWords.TryGetValue(areaLower, out var compoundReplacement)
                ? compoundReplacement
                : areaLower;
        }

        // Step 2: Ensure "azure-" prefix
        if (!brandPrefix.StartsWith("azure-", StringComparison.OrdinalIgnoreCase))
        {
            brandPrefix = $"azure-{brandPrefix}";
        }

        // Step 3: Clean remaining command parts (compound word expansion + stop word removal)
        if (commandParts.Length <= 1)
            return brandPrefix;

        var remainingParts = string.Join("-", commandParts.Skip(1)).ToLowerInvariant();
        var cleanedRemaining = CleanParts(remainingParts, compoundWords, stopWords);

        return string.IsNullOrEmpty(cleanedRemaining)
            ? brandPrefix
            : $"{brandPrefix}-{cleanedRemaining}";
    }

    /// <summary>
    /// Builds the canonical base filename asynchronously, loading data files automatically.
    /// Convenience method when callers don't already have the data loaded.
    /// </summary>
    public static async Task<string> BuildBaseFileNameAsync(string command)
    {
        var ctx = await FileNameContext.CreateAsync();
        return BuildBaseFileName(command, ctx);
    }

    // ── Typed filename builders (context overloads) ─────────────────────

    /// <summary>Builds the full filename for an annotation file.</summary>
    public static string BuildAnnotationFileName(string command, FileNameContext ctx)
        => $"{BuildBaseFileName(command, ctx)}-annotations.md";

    /// <summary>Builds the full filename for a parameter file.</summary>
    public static string BuildParameterFileName(string command, FileNameContext ctx)
        => $"{BuildBaseFileName(command, ctx)}-parameters.md";

    /// <summary>Builds the full filename for an example prompts file.</summary>
    public static string BuildExamplePromptsFileName(string command, FileNameContext ctx)
        => $"{BuildBaseFileName(command, ctx)}-example-prompts.md";

    /// <summary>Builds the full filename for an example prompts input prompt file.</summary>
    public static string BuildInputPromptFileName(string command, FileNameContext ctx)
        => $"{BuildBaseFileName(command, ctx)}-input-prompt.md";

    /// <summary>Builds the full filename for a raw output file.</summary>
    public static string BuildRawOutputFileName(string command, FileNameContext ctx)
        => $"{BuildBaseFileName(command, ctx)}-raw-output.txt";

    /// <summary>Builds the full filename for a raw/composed tool file (no content-type suffix).</summary>
    public static string BuildToolFileName(string command, FileNameContext ctx)
        => $"{BuildBaseFileName(command, ctx)}.md";

    // ── Typed filename builders (explicit params overloads) ─────────────

    /// <summary>Builds the full filename for an annotation file.</summary>
    public static string BuildAnnotationFileName(
        string command,
        Dictionary<string, BrandMapping> brandMappings,
        Dictionary<string, string> compoundWords,
        HashSet<string> stopWords)
        => $"{BuildBaseFileName(command, brandMappings, compoundWords, stopWords)}-annotations.md";

    /// <summary>Builds the full filename for a parameter file.</summary>
    public static string BuildParameterFileName(
        string command,
        Dictionary<string, BrandMapping> brandMappings,
        Dictionary<string, string> compoundWords,
        HashSet<string> stopWords)
        => $"{BuildBaseFileName(command, brandMappings, compoundWords, stopWords)}-parameters.md";

    /// <summary>Builds the full filename for an example prompts file.</summary>
    public static string BuildExamplePromptsFileName(
        string command,
        Dictionary<string, BrandMapping> brandMappings,
        Dictionary<string, string> compoundWords,
        HashSet<string> stopWords)
        => $"{BuildBaseFileName(command, brandMappings, compoundWords, stopWords)}-example-prompts.md";

    /// <summary>Builds the full filename for an example prompts input prompt file.</summary>
    public static string BuildInputPromptFileName(
        string command,
        Dictionary<string, BrandMapping> brandMappings,
        Dictionary<string, string> compoundWords,
        HashSet<string> stopWords)
        => $"{BuildBaseFileName(command, brandMappings, compoundWords, stopWords)}-input-prompt.md";

    /// <summary>Builds the full filename for a raw output file.</summary>
    public static string BuildRawOutputFileName(
        string command,
        Dictionary<string, BrandMapping> brandMappings,
        Dictionary<string, string> compoundWords,
        HashSet<string> stopWords)
        => $"{BuildBaseFileName(command, brandMappings, compoundWords, stopWords)}-raw-output.txt";

    /// <summary>Builds the full filename for a raw/composed tool file (no content-type suffix).</summary>
    public static string BuildToolFileName(
        string command,
        Dictionary<string, BrandMapping> brandMappings,
        Dictionary<string, string> compoundWords,
        HashSet<string> stopWords)
        => $"{BuildBaseFileName(command, brandMappings, compoundWords, stopWords)}.md";

    // ── Internal helpers ────────────────────────────────────────────────

    /// <summary>
    /// Cleans hyphen-separated parts by expanding compound words and removing stop words.
    /// </summary>
    private static string CleanParts(
        string hyphenatedParts,
        Dictionary<string, string> compoundWords,
        HashSet<string> stopWords)
    {
        var parts = hyphenatedParts.Split('-');
        var cleanedParts = new List<string>();

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part))
                continue;

            var lowerPart = part.ToLowerInvariant();
            if (compoundWords.TryGetValue(lowerPart, out var expanded))
            {
                // Expand compound word, then filter stop words from the pieces
                foreach (var subPart in expanded.Split('-'))
                {
                    var lowerSubPart = subPart.ToLowerInvariant();
                    if (!stopWords.Contains(lowerSubPart))
                        cleanedParts.Add(lowerSubPart);
                }
            }
            else
            {
                if (!stopWords.Contains(lowerPart))
                    cleanedParts.Add(lowerPart);
            }
        }

        return string.Join("-", cleanedParts);
    }
}
