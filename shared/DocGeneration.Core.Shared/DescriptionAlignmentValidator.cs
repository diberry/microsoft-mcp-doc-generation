// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Validates alignment between MCP (NLP) descriptions and CLI descriptions.
/// Computes word-overlap similarity and flags divergence.
/// </summary>
public static class DescriptionAlignmentValidator
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "is", "in", "for", "of", "to", "and", "with",
        "that", "this", "are", "be", "it", "on", "or", "as", "by", "at",
        "from", "was", "were", "been", "has", "have", "had", "not", "but",
        "can", "will", "do", "does", "did", "its", "you", "your"
    };

    private const double WarningThreshold = 0.6;
    private const double ErrorThreshold = 0.4;

    public record AlignmentResult(
        bool IsValid,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> Warnings);

    /// <summary>
    /// Validates alignment between MCP and CLI descriptions for a set of tools.
    /// </summary>
    /// <param name="mcpDescriptions">NLP source descriptions keyed by tool command</param>
    /// <param name="cliDescriptions">CLI-adapted descriptions keyed by tool command</param>
    public static AlignmentResult Validate(
        IReadOnlyDictionary<string, string> mcpDescriptions,
        IReadOnlyDictionary<string, string> cliDescriptions)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var (command, mcpDesc) in mcpDescriptions)
        {
            if (!cliDescriptions.TryGetValue(command, out var cliDesc))
                continue;

            if (string.IsNullOrWhiteSpace(mcpDesc) || string.IsNullOrWhiteSpace(cliDesc))
                continue;

            var similarity = ComputeWordOverlapSimilarity(mcpDesc, cliDesc);

            if (similarity < ErrorThreshold)
            {
                errors.Add($"[{command}] Descriptions diverged too far (similarity: {similarity:P0}). " +
                    $"MCP: \"{Truncate(mcpDesc, 80)}\" | CLI: \"{Truncate(cliDesc, 80)}\"");
            }
            else if (similarity < WarningThreshold)
            {
                warnings.Add($"[{command}] Descriptions may be drifting (similarity: {similarity:P0}). " +
                    $"MCP: \"{Truncate(mcpDesc, 80)}\" | CLI: \"{Truncate(cliDesc, 80)}\"");
            }
        }

        return new AlignmentResult(errors.Count == 0, errors, warnings);
    }

    /// <summary>
    /// Computes word-overlap similarity between two descriptions.
    /// Returns ratio of shared significant words to total significant words.
    /// </summary>
    internal static double ComputeWordOverlapSimilarity(string text1, string text2)
    {
        var words1 = ExtractSignificantWords(text1);
        var words2 = ExtractSignificantWords(text2);

        if (words1.Count == 0 && words2.Count == 0)
            return 1.0;

        var allWords = new HashSet<string>(words1, StringComparer.OrdinalIgnoreCase);
        allWords.UnionWith(words2);

        if (allWords.Count == 0)
            return 1.0;

        var shared = new HashSet<string>(words1, StringComparer.OrdinalIgnoreCase);
        shared.IntersectWith(words2);

        return (double)shared.Count / allWords.Count;
    }

    private static HashSet<string> ExtractSignificantWords(string text)
    {
        var words = text.Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '(', ')', '[', ']', '{', '}', '"', '\'', '`' },
            StringSplitOptions.RemoveEmptyEntries);

        var significant = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var word in words)
        {
            var cleaned = word.Trim('-', '_');
            if (cleaned.Length > 1 && !StopWords.Contains(cleaned))
                significant.Add(cleaned);
        }
        return significant;
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "...";
}
