// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Shared;

/// <summary>
/// Token-aware coverage evaluator using only manifest-authorized aliases.
/// Never uses Contains/substring matching, N-of-M word matching, or morphological variants.
/// </summary>
public static class CanonicalCoverageEvaluator
{
    // Matches placeholder tokens: <...>, {...}, [...], `...`
    private static readonly Regex PlaceholderRegex = new(
        @"<([^<>]+)>|\{([^{}]+)\}|\[([^\[\]]+)\]|`([^`]+)`",
        RegexOptions.Compiled);

    /// <summary>
    /// Evaluates coverage for all parameters in a manifest.
    /// </summary>
    public static CoverageResult EvaluateParameterCoverage(
        IReadOnlyList<string> renderedPrompts,
        CanonicalParameterManifest manifest)
    {
        var results = new List<SingleParameterCoverage>(manifest.Parameters.Count);
        var allRequiredCovered = true;

        foreach (var param in manifest.Parameters)
        {
            var coverage = EvaluateSingleParameter(renderedPrompts, param, manifest.PlaceholderAliasIndex);
            results.Add(coverage);

            if (param.Required && coverage.Verdict != CoverageVerdict.Concrete && coverage.Verdict != CoverageVerdict.AuthorizedPlaceholder)
            {
                allRequiredCovered = false;
            }
        }

        return new CoverageResult(results, allRequiredCovered);
    }

    /// <summary>
    /// Evaluates coverage for a single parameter against rendered prompts.
    /// Uses ONLY manifest-authorized aliases for placeholder matching.
    /// </summary>
    public static SingleParameterCoverage EvaluateSingleParameter(
        IReadOnlyList<string> renderedPrompts,
        CanonicalParameterEntry parameter,
        IReadOnlyDictionary<string, string> placeholderAliasIndex)
    {
        // Check for concrete value coverage first (quoted values or natural references)
        for (int i = 0; i < renderedPrompts.Count; i++)
        {
            var prompt = renderedPrompts[i];
            if (string.IsNullOrWhiteSpace(prompt)) continue;

            var concreteMatch = CheckConcreteValue(prompt, parameter);
            if (concreteMatch is not null)
            {
                return new SingleParameterCoverage(
                    parameter.CanonicalName,
                    CoverageVerdict.Concrete,
                    concreteMatch,
                    i);
            }
        }

        // Check for authorized placeholder matches
        for (int i = 0; i < renderedPrompts.Count; i++)
        {
            var prompt = renderedPrompts[i];
            if (string.IsNullOrWhiteSpace(prompt)) continue;

            var placeholderMatch = CheckAuthorizedPlaceholder(prompt, parameter, placeholderAliasIndex);
            if (placeholderMatch is not null)
            {
                return new SingleParameterCoverage(
                    parameter.CanonicalName,
                    CoverageVerdict.AuthorizedPlaceholder,
                    placeholderMatch,
                    i);
            }
        }

        return new SingleParameterCoverage(parameter.CanonicalName, CoverageVerdict.Missing, null, null);
    }

    /// <summary>
    /// Checks if a prompt contains a concrete (non-placeholder) value for the parameter.
    /// Checks for:
    /// 1. Quoted values following display name variants (e.g., "for account 'myaccount123'")
    /// 2. Word-boundary matches of the canonical name or display aliases in non-placeholder prose
    /// </summary>
    private static string? CheckConcreteValue(string prompt, CanonicalParameterEntry parameter)
    {
        // Strip placeholder tokens to avoid matching inside them
        var strippedPrompt = PlaceholderRegex.Replace(prompt, " ");

        var allAliases = parameter.DisplayAliases
            .Concat(new[] { parameter.CanonicalName })
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var alias in allAliases)
        {
            var naturalAlias = alias.Replace('-', ' ');

            // Pattern 1: alias followed by a quoted value
            var pattern = $@"(?<!\w){Regex.Escape(naturalAlias)}(?:\s+named|\s+called)?\s+'([^']+)'";
            var match = Regex.Match(prompt, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Value;
            }

            // Pattern 2: word-boundary match in non-placeholder prose (concrete reference)
            var wordPattern = $@"(?<!\w){Regex.Escape(naturalAlias)}(?!\w)";
            match = Regex.Match(strippedPrompt, wordPattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a prompt contains an authorized placeholder that exactly matches
    /// an entry in the placeholder alias index for this parameter.
    /// </summary>
    private static string? CheckAuthorizedPlaceholder(
        string prompt,
        CanonicalParameterEntry parameter,
        IReadOnlyDictionary<string, string> placeholderAliasIndex)
    {
        var matches = PlaceholderRegex.Matches(prompt);
        foreach (Match match in matches)
        {
            var rawToken = match.Groups[1].Success ? match.Groups[1].Value :
                           match.Groups[2].Success ? match.Groups[2].Value :
                           match.Groups[3].Success ? match.Groups[3].Value :
                           match.Groups[4].Value;

            // Look up in the index — must exactly match an authorized alias
            // Check raw token first (preserves original form in evidence)
            if (placeholderAliasIndex.TryGetValue(rawToken, out var rawOwner) &&
                string.Equals(rawOwner, parameter.CanonicalName, StringComparison.OrdinalIgnoreCase))
            {
                return rawToken;
            }

            // Then check normalized token
            var normalizedToken = CanonicalParameterNormalizer.Normalize(rawToken);
            if (string.IsNullOrEmpty(normalizedToken)) continue;

            if (placeholderAliasIndex.TryGetValue(normalizedToken, out var ownerCanonical) &&
                string.Equals(ownerCanonical, parameter.CanonicalName, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedToken;
            }
        }

        return null;
    }
}
