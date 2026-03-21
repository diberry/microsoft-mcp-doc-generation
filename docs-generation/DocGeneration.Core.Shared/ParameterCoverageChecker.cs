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
                    || words.Count(word => placeholderSlug.Contains(word, StringComparison.Ordinal)) >= requiredWordMatches)
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
                    if (words.Count(word => innerTokens.Contains(word, StringComparer.Ordinal))
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
                if (Regex.IsMatch(tail, "^\\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\\s*'[^']+'")
                    || Regex.IsMatch(tail, "^\\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\\s*`[^`]+`")
                    || Regex.IsMatch(tail, "^\\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\\s*https?://\\S+")
                    || Regex.IsMatch(tail, "^\\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\\s*\\[(?!\\s*<)(?!\\s*\\{\\s*[^'\"\\s]).+\\]")
                    || Regex.IsMatch(tail, "^\\s*(?:set to|named|name|with|at|for|in|of|is|=|:)?\\s*\\{(?!\\s*[<\\{]).+\\}"))
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

            if (!covered && totalRequiredParameters == 1 && placeholders.Length == 0)
            {
                if (Regex.IsMatch(trimmedPrompt, "'[^'<>{}\\[\\]]+'")
                    || Regex.IsMatch(trimmedPrompt, "`[^`<>{}\\[\\]]+`")
                    || Regex.IsMatch(trimmedPrompt, "https?://\\S+"))
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

        return new PromptCoverage(covered, placeholderDetected);
    }

    public static string ConvertToSlug(string text)
    {
        var clean = RemoveMarkup(text);
        if (string.IsNullOrWhiteSpace(clean))
        {
            return string.Empty;
        }

        var slug = Regex.Replace(clean.ToLowerInvariant(), "[^a-z0-9]+", "-");
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
}

public sealed record PromptCoverage(bool Covered, bool PlaceholderDetected);
