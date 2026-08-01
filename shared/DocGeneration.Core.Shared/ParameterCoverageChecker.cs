using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Shared;

/// <summary>
/// Checks whether example prompts contain concrete (non-placeholder) values
/// for required parameters.
/// </summary>
public static class ParameterCoverageChecker
{
    public static PromptCoverage GetConcretePromptCoverage(IReadOnlyList<string> examplePrompts, string parameterName, int totalRequiredParameters)
        => GetConcretePromptCoverage(examplePrompts, parameterName, totalRequiredParameters, parameterDescription: null);

    /// <summary>
    /// Checks whether the example prompts contain a concrete (non-placeholder) value for the
    /// required parameter. When <paramref name="parameterDescription"/> declares a closed set of
    /// allowed values (for example "Available options: 'storage_storageaccounts', ...") this method
    /// additionally treats the parameter as covered when a prompt references one of those allowed
    /// values by name (for example a prompt mentioning "Storage Account"). Enum matching is purely
    /// additive: it can only turn an otherwise-uncovered enum parameter into covered, never the
    /// reverse, and only affects parameters whose description enumerates a closed option set.
    /// </summary>
    public static PromptCoverage GetConcretePromptCoverage(IReadOnlyList<string> examplePrompts, string parameterName, int totalRequiredParameters, string? parameterDescription)
    {
        var slug = ConvertToSlug(parameterName);
        var words = slug.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var wordPattern = string.Join("[-_ ]+", words.Select(Regex.Escape));
        var variantList = new List<string>();

        foreach (var variant in new[]
        {
            parameterName.ToLowerInvariant(),
            string.Join(' ', words),
            string.Join('-', words),
            string.Join('_', words),
        })
        {
            if (!string.IsNullOrWhiteSpace(variant))
            {
                variantList.Add(variant);
            }
        }

        if (words.Length > 1 && new[] { "name", "text", "array", "value" }.Contains(words[^1], StringComparer.Ordinal))
        {
            var baseWords = words[..^1];
            foreach (var variant in new[]
            {
                string.Join(' ', baseWords),
                string.Join('-', baseWords),
                string.Join('_', baseWords),
            })
            {
                if (!string.IsNullOrWhiteSpace(variant))
                {
                    variantList.Add(variant);
                }
            }
        }

        if (words.Length > 0 && string.Equals(words[^1], "text", StringComparison.Ordinal))
        {
            variantList.Add("text");
        }

        if (words.Length > 0 && string.Equals(words[^1], "array", StringComparison.Ordinal))
        {
            variantList.Add(words[0]);
            variantList.Add($"{words[0]}s");
        }

        // Extract abbreviations from parentheses: (VMSS), (AKS), etc.
        foreach (Match abbr in Regex.Matches(parameterName, @"\(([A-Z]{2,})\)"))
        {
            variantList.Add(abbr.Groups[1].Value.ToLowerInvariant());
        }

        // Add common abbreviation expansions
        var abbreviationExpansions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "param", new[] { "parameter", "parameters" } },
            { "config", new[] { "configuration", "configurations" } },
            { "env", new[] { "environment", "environments" } },
            { "msg", new[] { "message", "messages" } },
        };
        if (abbreviationExpansions.TryGetValue(slug, out var expansions))
        {
            variantList.AddRange(expansions);
        }

        // Add plural and past-tense morphological forms for single-word slugs
        if (words.Length == 1)
        {
            variantList.Add(slug + "s");
            variantList.Add(slug + "d");
        }

        // For multi-word params (e.g. "database-server"), add individual constituent
        // words as variants so prompts like "server 'my-host'" can match.
        if (words.Length > 1)
        {
            foreach (var word in words)
            {
                if (word.Length >= 3)
                {
                    variantList.Add(word);
                }
            }
        }

        var variants = variantList
            .Where(variant => !string.IsNullOrWhiteSpace(variant))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var placeholderDetected = false;
        var covered = false;
        foreach (var examplePrompt in examplePrompts)
        {
            if (string.IsNullOrWhiteSpace(examplePrompt))
            {
                continue;
            }

            var trimmedPrompt = examplePrompt.Trim();
            var lowerPrompt = trimmedPrompt.ToLowerInvariant();
            var placeholders = Regex.Matches(trimmedPrompt, "<[^>]+>|\\{[^}]+\\}|\\[[^\\]]+\\]|`[^`]+`")
                .Select(match => match.Value)
                .ToArray();

            foreach (var placeholder in placeholders)
            {
                // Strip outer bracket pair (<…>, {…}, […]) so ConvertToSlug sees the inner text
                var inner = placeholder.Length >= 2 ? placeholder[1..^1] : placeholder;
                // Strip any remaining nested delimiters (handles double-wrapped like `<account>`)
                inner = inner.TrimStart('<', '{', '[').TrimEnd('>', '}', ']');
                var placeholderSlug = ConvertToSlug(inner);
                var requiredWordMatches = Math.Min(Math.Max(words.Length, 1), 2);
                if (placeholderSlug == slug
                    || placeholderSlug.Contains(slug, StringComparison.Ordinal)
                    || CountRequiredWordMatches(words, word => placeholderSlug.Contains(word, StringComparison.Ordinal)) >= requiredWordMatches)
                {
                    placeholderDetected = true;
                }

                // Semantic fallback: word-level match on raw inner text (before slugifying).
                // Catches descriptive placeholders like <key_name> for parameter "Key"
                // where the parameter word appears as a discrete token in the placeholder.
                if (!placeholderDetected)
                {
                    var innerTokens = Regex.Split(inner.ToLowerInvariant(), "[^a-z0-9]+")
                        .Where(t => t.Length > 0)
                        .ToArray();
                    if (CountRequiredWordMatches(words, word => innerTokens.Contains(word, StringComparer.Ordinal))
                        >= requiredWordMatches)
                    {
                        placeholderDetected = true;
                    }
                }
            }

            var foundVariant = false;
            var matchIndex = -1;
            foreach (var variant in variants)
            {
                var lowerVariant = variant.ToLowerInvariant();
                // Defect 1 fix: single-word variants use word boundary to avoid substring matches
                if (!lowerVariant.Contains(' ') && !lowerVariant.Contains('-') && !lowerVariant.Contains('_'))
                {
                    var m = Regex.Match(lowerPrompt, $@"\b{Regex.Escape(lowerVariant)}\b");
                    if (m.Success)
                    {
                        foundVariant = true;
                        matchIndex = m.Index + m.Length;
                        // Extend past common morphological suffixes if present
                        var suffixTail = lowerPrompt[matchIndex..];
                        var suffixMatch = Regex.Match(suffixTail, @"^(ing|ed|er|d|s)\b");
                        if (suffixMatch.Success)
                        {
                            matchIndex += suffixMatch.Length;
                        }
                        break;
                    }
                }
                else
                {
                    var currentIndex = lowerPrompt.IndexOf(lowerVariant, StringComparison.Ordinal);
                    if (currentIndex >= 0)
                    {
                        foundVariant = true;
                        matchIndex = currentIndex + lowerVariant.Length;
                        break;
                    }
                }
            }

            if (!foundVariant && !string.IsNullOrWhiteSpace(wordPattern))
            {
                var wordMatch = Regex.Match(lowerPrompt, $"(?i)\\b{wordPattern}\\b");
                if (wordMatch.Success)
                {
                    foundVariant = true;
                    matchIndex = wordMatch.Index + wordMatch.Length;
                }
            }

            if (foundVariant && matchIndex >= 0)
            {
                var tail = trimmedPrompt[Math.Min(matchIndex, trimmedPrompt.Length)..];
                // Allow up to 2 intermediate words between the parameter name and the value
                // (e.g. "app service 'my-app'" where "service" sits between "app" and the value).
                const string cp = "(?:\\w+\\s+){0,2}(?:set to|named|name|with|at|for|in|of|is|=|:)?";
                if (Regex.IsMatch(tail, "^\\s*" + cp + "\\s*'[^']+'")
                    || Regex.IsMatch(tail, "^\\s*" + cp + "\\s*`[^`]+`")
                    || Regex.IsMatch(tail, "^\\s*" + cp + "\\s*https?://\\S+")
                    || Regex.IsMatch(tail, "^\\s*" + cp + "\\s*\\[(?!\\s*<)(?!\\s*\\{\\s*[^'\"\\s]).+\\]")
                    || Regex.IsMatch(tail, "^\\s*" + cp + "\\s*\\{(?!\\s*[<\\{]).+\\}"))
                {
                    covered = true;
                    break;
                }

                // Defect 3 fix: multi-word structural parameters (3+ words) at sentence end
                if (words.Length >= 3 && string.IsNullOrWhiteSpace(tail))
                {
                    covered = true;
                    break;
                }
            }

            // Fallback for single-word resource identifier params with low param count
            if (!covered && words.Length == 1 && totalRequiredParameters <= 2 && placeholders.Length == 0)
            {
                var nameLikeParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "name", "key", "id", "app", "param", "tag", "role", "type", "path",
                };
                if (nameLikeParams.Contains(slug))
                {
                    if (Regex.IsMatch(trimmedPrompt, "'[^'<>{}\\[\\]]+'")
                        || Regex.IsMatch(trimmedPrompt, "`[^`<>{}\\[\\]]+`"))
                    {
                        covered = true;
                        break;
                    }
                }
            }
        }

        // Enum-aware coverage (additive only): if the parameter description enumerates a closed set
        // of allowed values and none of the name-based checks matched, treat the parameter as covered
        // when any prompt references one of the allowed values. This resolves false positives where an
        // authoritative prompt names a concrete option (e.g. "Storage Account") rather than repeating
        // the parameter name. It can only flip covered from false to true, so it never masks a genuine
        // miss on a non-enum parameter.
        if (!covered)
        {
            var allowedValues = ParseAllowedValues(parameterDescription);
            if (allowedValues.Count > 0)
            {
                foreach (var examplePrompt in examplePrompts)
                {
                    if (string.IsNullOrWhiteSpace(examplePrompt))
                    {
                        continue;
                    }

                    var promptTokenNGrams = BuildPromptTokenNGrams(examplePrompt);
                    if (PromptReferencesAllowedValue(promptTokenNGrams, allowedValues))
                    {
                        covered = true;
                        break;
                    }
                }
            }
        }

        return new PromptCoverage(covered, placeholderDetected);
    }

    /// <summary>
    /// Parses a closed set of allowed values from a parameter description. Only explicit closed-enum
    /// trigger phrases ("Available options:", "Allowed values:", "Valid values:", "Must be one of:",
    /// "One of:", "Options:") are recognized; open-ended example phrasing ("e.g.", "for example",
    /// "such as", "typical values") is intentionally ignored so free-text parameters are not treated
    /// as enums. Returns the quoted values that follow the trigger phrase.
    /// </summary>
    public static IReadOnlyList<string> ParseAllowedValues(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return Array.Empty<string>();
        }

        var clean = RemoveMarkup(description);
        var trigger = Regex.Match(
            clean,
            "(?i)\\b(available options|allowed values|valid values|must be one of|one of the following|one of|options)\\s*:?\\s*");
        if (!trigger.Success)
        {
            return Array.Empty<string>();
        }

        var tail = clean[(trigger.Index + trigger.Length)..];
        var values = new List<string>();
        foreach (Match match in Regex.Matches(tail, "'([^']+)'|\"([^\"]+)\""))
        {
            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value.Trim());
            }
        }

        return values;
    }

    /// <summary>
    /// Builds the set of concatenated adjacent-token n-grams (1..maxN words) for a prompt, each
    /// reduced to lowercase alphanumerics. Enum matching compares candidates against these n-grams by
    /// equality, which enforces word boundaries: a multi-word display value ("Storage Account" →
    /// "storageaccount") still matches, but a generic segment ("server") can no longer match inside an
    /// unrelated word ("observer") the way a raw substring scan of the fully collapsed prompt would.
    /// </summary>
    private static HashSet<string> BuildPromptTokenNGrams(string prompt, int maxN = 4)
    {
        var tokens = Regex.Split(prompt.ToLowerInvariant(), "[^a-z0-9]+")
            .Where(token => token.Length > 0)
            .ToArray();
        var nGrams = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < tokens.Length; i++)
        {
            var accumulated = string.Empty;
            for (var n = 0; n < maxN && i + n < tokens.Length; n++)
            {
                accumulated += tokens[i + n];
                nGrams.Add(accumulated);
            }
        }

        return nGrams;
    }

    private static bool PromptReferencesAllowedValue(IReadOnlyCollection<string> promptTokenNGrams, IReadOnlyList<string> allowedValues)
    {
        foreach (var value in allowedValues)
        {
            foreach (var candidate in GetEnumMatchCandidates(value))
            {
                if (candidate.Length >= 5 && promptTokenNGrams.Contains(candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Produces match candidates for an allowed enum value. Azure resource-type options follow a
    /// "provider_resourcetype" convention (e.g. "storage_storageaccounts"), so both the whole collapsed
    /// value and the final segment (the resource type) are yielded, each with a de-pluralized form.
    /// Generic leading provider segments (e.g. "storage", "web") are intentionally excluded to limit
    /// incidental matches. Candidates shorter than five characters are dropped by the caller. Candidates
    /// are matched against prompt token n-grams by equality (see <see cref="BuildPromptTokenNGrams"/>),
    /// so they align to word boundaries rather than matching as raw substrings.
    /// </summary>
    private static IEnumerable<string> GetEnumMatchCandidates(string value)
    {
        var lower = value.ToLowerInvariant();
        var segments = Regex.Split(lower, "[^a-z0-9]+")
            .Where(segment => segment.Length > 0)
            .ToArray();
        if (segments.Length == 0)
        {
            yield break;
        }

        var whole = string.Concat(segments);
        foreach (var candidate in Depluralize(whole))
        {
            yield return candidate;
        }

        var last = segments[^1];
        if (!string.Equals(last, whole, StringComparison.Ordinal))
        {
            foreach (var candidate in Depluralize(last))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<string> Depluralize(string word)
    {
        yield return word;
        if (word.Length > 5 && word.EndsWith('s'))
        {
            yield return word[..^1];
        }
    }

    public static string ConvertToSlug(string text)
    {
        var clean = RemoveMarkup(text);
        if (string.IsNullOrWhiteSpace(clean))
        {
            return string.Empty;
        }

        // Split camelCase/PascalCase before lowercasing so "outputAudio" → "output-audio"
        var split = Regex.Replace(clean, "([a-z])([A-Z])", "$1-$2");
        var slug = Regex.Replace(split.ToLowerInvariant(), "[^a-z0-9]+", "-");
        return slug.Trim('-');
    }

    public static string RemoveMarkup(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var clean = text.Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal);
        clean = Regex.Replace(clean, "<[^>]+>", string.Empty);
        clean = Regex.Replace(clean, "\\s+", " ");
        return clean.Trim();
    }

    private static int CountRequiredWordMatches(IReadOnlyList<string> requiredWords, Func<string, bool> matches)
        => requiredWords.Count(word => GetWordVariants(word).Any(matches));

    private static IEnumerable<string> GetWordVariants(string word)
    {
        yield return word;

        if (string.Equals(word, "ids", StringComparison.Ordinal))
        {
            yield return "id";
        }
        else if (word.Length > 3 && word.EndsWith('s'))
        {
            yield return word[..^1];
        }
    }
}

public sealed record PromptCoverage(bool Covered, bool PlaceholderDetected);
