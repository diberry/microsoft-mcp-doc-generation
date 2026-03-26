// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Shared;

/// <summary>
/// Generates deterministic, unique H2 headings for MCP tool documentation.
/// Uses verb mapping, abbreviation expansion, and disambiguation to ensure
/// all headings within a namespace are unique and human-readable.
/// </summary>
public static class DeterministicH2HeadingGenerator
{
    private static readonly Dictionary<string, string> VerbMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["list"] = "Get",
        ["show"] = "Get",
        ["get"] = "Get",
        ["describe"] = "Get",
        ["create"] = "Create",
        ["add"] = "Create",
        ["update"] = "Update",
        ["modify"] = "Update",
        ["set"] = "Update",
        ["delete"] = "Delete",
        ["remove"] = "Delete",
        ["query"] = "Query",
        ["search"] = "Query",
        ["retrieve"] = "Retrieve",
        ["check-name-availability"] = "Check",
    };

    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vm"] = "virtual machine",
        ["vmss"] = "virtual machine scale set",
        ["acr"] = "container registry",
        ["aks"] = "Kubernetes cluster",
        ["kv"] = "key vault",
        ["peconnection"] = "private endpoint connection",
        ["rec"] = "recommendations",
    };

    /// <summary>
    /// Proper nouns and acronyms whose casing must be preserved in sentence case headings.
    /// Keyed by case-insensitive lookup; values are the canonical casing.
    /// </summary>
    private static readonly Dictionary<string, string> ProperNounMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Azure"] = "Azure",
        ["SQL"] = "SQL",
        ["PostgreSQL"] = "PostgreSQL",
        ["MySQL"] = "MySQL",
        ["Cosmos"] = "Cosmos",
        ["DB"] = "DB",
        ["Kubernetes"] = "Kubernetes",
        ["AKS"] = "AKS",
        ["API"] = "API",
        ["CLI"] = "CLI",
        ["VM"] = "VM",
        ["VMSS"] = "VMSS",
        ["DNS"] = "DNS",
        ["IP"] = "IP",
        ["URL"] = "URL",
        ["HTTP"] = "HTTP",
        ["HTTPS"] = "HTTPS",
        ["RBAC"] = "RBAC",
        ["OAuth"] = "OAuth",
        ["JWT"] = "JWT",
        ["TLS"] = "TLS",
        ["SSL"] = "SSL",
        ["MCP"] = "MCP",
        ["AI"] = "AI",
        ["Entra"] = "Entra",
        ["ID"] = "ID",
        ["Bicep"] = "Bicep",
        ["Terraform"] = "Terraform",
        ["Redis"] = "Redis",
        ["SignalR"] = "SignalR",
        ["Grafana"] = "Grafana",
        ["Kusto"] = "Kusto",
        ["Lustre"] = "Lustre",
        ["DevOps"] = "DevOps",
    };

    /// <summary>
    /// Public read-only view of proper nouns for test assertions.
    /// </summary>
    public static readonly IReadOnlySet<string> ProperNouns =
        new HashSet<string>(ProperNounMap.Values, StringComparer.Ordinal);

    // Words to ignore when extracting disambiguation tokens from descriptions
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "of", "for", "in", "to", "and", "or", "is", "are",
        "that", "this", "with", "from", "by", "on", "at", "it", "its", "be",
        "was", "were", "been", "being", "have", "has", "had", "do", "does",
        "did", "will", "would", "could", "should", "may", "might", "can",
        "shall", "new", "all", "if", "not", "no", "get", "set", "list",
        "create", "update", "delete", "remove", "add", "show", "describe",
        "check", "specific", "details", "existing", "available",
    };

    /// <summary>
    /// Generate a single heading from command + description.
    /// </summary>
    public static string GenerateHeading(string command, string? description)
    {
        var segments = (command ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return "Tool";

        string namespaceName = segments[0];
        string verb;
        string[] resourceSegments;

        if (segments.Length == 1)
        {
            // Single segment: try to extract verb from description
            verb = ExtractVerbFromDescription(description);
            resourceSegments = Array.Empty<string>();
        }
        else if (segments.Length == 2)
        {
            // "fileshares limits" — second segment could be verb or noun
            var seg = segments[1];
            if (VerbMap.ContainsKey(seg))
            {
                // It's a verb: extract resource from description
                verb = seg;
                resourceSegments = Array.Empty<string>();
            }
            else if (Abbreviations.ContainsKey(seg))
            {
                // It's an abbreviation for a resource noun
                verb = ExtractVerbFromDescription(description);
                resourceSegments = new[] { seg };
            }
            else
            {
                // Treat as a noun/topic, extract verb from description
                verb = ExtractVerbFromDescription(description);
                resourceSegments = new[] { seg };
            }
        }
        else
        {
            // "compute vm create" → verb="create", resource=["vm"]
            verb = segments[^1];
            resourceSegments = segments[1..^1];
        }

        var displayVerb = MapVerb(verb);
        var resourcePhrase = BuildResourcePhrase(namespaceName, resourceSegments, verb, description);

        // For "list" verb, pluralize the resource
        var needsPlural = verb.Equals("list", StringComparison.OrdinalIgnoreCase);
        if (needsPlural && !string.IsNullOrEmpty(resourcePhrase))
        {
            resourcePhrase = Pluralize(resourcePhrase);
        }

        var heading = string.IsNullOrWhiteSpace(resourcePhrase)
            ? displayVerb
            : $"{displayVerb} {resourcePhrase}";

        return ToSentenceCase(Truncate(heading, 7));
    }

    /// <summary>
    /// Generate all headings for a namespace, ensuring uniqueness.
    /// </summary>
    public static Dictionary<string, string> GenerateHeadings(
        IEnumerable<(string command, string? description)> tools)
    {
        var toolList = tools.ToList();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        // Phase 1: Generate initial headings
        foreach (var (command, description) in toolList)
        {
            result[command] = GenerateHeading(command, description);
        }

        // Phase 2: Disambiguate duplicates
        var duplicateGroups = result
            .GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in duplicateGroups)
        {
            var entries = group.ToList();
            var toolLookup = toolList.ToDictionary(t => t.command, t => t.description, StringComparer.Ordinal);

            // Try to disambiguate using resource segments and description
            DisambiguateGroup(entries, toolLookup, result);
        }

        // Phase 3: Enforce sentence case on all headings (including disambiguated ones)
        foreach (var key in result.Keys.ToList())
        {
            result[key] = ToSentenceCase(result[key]);
        }

        return result;
    }

    private static string MapVerb(string verb)
    {
        if (VerbMap.TryGetValue(verb, out var mapped))
            return mapped;

        // Title-case the verb, handling hyphenated verbs
        return TitleCase(verb.Replace("-", " "));
    }

    private static string BuildResourcePhrase(string namespaceName, string[] resourceSegments, string verb, string? description)
    {
        if (resourceSegments.Length > 0)
        {
            var expanded = resourceSegments.Select(ExpandAbbreviation);
            var phrase = string.Join(" ", expanded);

            // If expanded resource is a single word, prepend namespace for context
            // (unless namespace is similar to the resource, e.g. "fileshares" ~ "fileshare")
            var wordCount = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (wordCount == 1 && !AreSimilar(namespaceName, phrase))
            {
                phrase = $"{namespaceName} {phrase}";
            }

            return phrase;
        }

        // No resource segments — extract from description
        return ExtractResourceFromDescription(verb, description);
    }

    private static bool AreSimilar(string a, string b)
    {
        return a.StartsWith(b, StringComparison.OrdinalIgnoreCase)
            || b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExpandAbbreviation(string segment)
    {
        return Abbreviations.TryGetValue(segment, out var expanded) ? expanded : segment;
    }

    private static string ExtractVerbFromDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "get";

        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0)
        {
            var firstWord = words[0].ToLowerInvariant();
            if (VerbMap.ContainsKey(firstWord))
                return firstWord;
        }
        return "get";
    }

    private static string ExtractResourceFromDescription(string verb, string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return verb;

        var words = description.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Skip past the verb in description, collect noun phrases
        var nouns = new List<string>();
        bool pastVerb = false;
        foreach (var word in words)
        {
            var clean = word.Trim(',', '.', ':', ';').ToLowerInvariant();
            if (!pastVerb)
            {
                if (VerbMap.ContainsKey(clean) || clean == verb.ToLowerInvariant())
                {
                    pastVerb = true;
                    continue;
                }
                // If first word isn't a verb, start collecting immediately
                pastVerb = true;
            }

            if (StopWords.Contains(clean))
                continue;

            // Stop at prepositions that start a clause
            if (clean is "for" or "in" or "from" or "about" or "using" or "within")
                break;

            nouns.Add(clean);
            if (nouns.Count >= 3)
                break;
        }

        if (nouns.Count > 0)
            return string.Join(" ", nouns);

        // Fallback: use the verb itself as noun
        return verb.Replace("-", " ");
    }

    private static void DisambiguateGroup(
        List<KeyValuePair<string, string>> entries,
        Dictionary<string, string?> toolLookup,
        Dictionary<string, string> result)
    {
        // Strategy 1: Use distinctive resource segments from command
        var commandSegmentsByTool = entries.Select(e =>
        {
            var segments = e.Key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return (entry: e, segments);
        }).ToList();

        // Find segments that differ between the duplicate commands
        // Compare all segments beyond the namespace (index 0) and verb (last)
        var allResourceParts = commandSegmentsByTool.Select(t =>
        {
            var s = t.segments;
            if (s.Length <= 2) return Array.Empty<string>();
            return s[1..^1];
        }).ToList();

        // Try disambiguating with resource sub-segments
        if (TryDisambiguateBySegments(entries, commandSegmentsByTool, result))
            return;

        // Strategy 2: Use description keywords
        TryDisambiguateByDescription(entries, toolLookup, result);
    }

    private static bool TryDisambiguateBySegments(
        List<KeyValuePair<string, string>> entries,
        List<(KeyValuePair<string, string> entry, string[] segments)> commandSegments,
        Dictionary<string, string> result)
    {
        // For each entry, find resource segments unique to that entry
        var segmentSets = commandSegments.Select(cs =>
        {
            var s = cs.segments;
            if (s.Length <= 2) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return new HashSet<string>(s[1..^1].Select(seg => ExpandAbbreviation(seg)), StringComparer.OrdinalIgnoreCase);
        }).ToList();

        // Check if the resource segments already differ
        bool allSame = segmentSets.All(s => s.SetEquals(segmentSets[0]));
        if (allSame)
            return false;

        // Resource segments differ — the base GenerateHeading should already produce different results
        // This means duplicates are from same resource with same verb — fall through to description
        return false;
    }

    private static void TryDisambiguateByDescription(
        List<KeyValuePair<string, string>> entries,
        Dictionary<string, string?> toolLookup,
        Dictionary<string, string> result)
    {
        // Extract distinctive keywords from each tool's description
        var distinctiveWords = new Dictionary<string, string>(StringComparer.Ordinal);

        // Collect all description words per entry
        var descWordSets = entries.Select(e =>
        {
            var desc = toolLookup.TryGetValue(e.Key, out var d) ? d : null;
            var words = ExtractKeywords(desc);
            return (entry: e, words);
        }).ToList();

        // Also extract command segments for additional disambiguation
        var cmdWordSets = entries.Select(e =>
        {
            var segments = e.Key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var allWords = new List<string>();
            foreach (var seg in segments.Skip(1)) // skip namespace
            {
                var expanded = ExpandAbbreviation(seg);
                allWords.AddRange(expanded.Split(' '));
            }
            return (entry: e, words: allWords);
        }).ToList();

        // Find the most distinctive word for each entry
        var allDescWords = descWordSets.SelectMany(d => d.words).ToList();

        for (int i = 0; i < entries.Count; i++)
        {
            var entryWords = descWordSets[i].words;
            var cmdWords = cmdWordSets[i].words.Select(w => w.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var currentHeading = entries[i].Value;
            var headingWords = currentHeading.Split(' ').Select(w => w.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Find words unique to this entry's description (or at least rare)
            string? bestWord = null;
            int bestScore = int.MaxValue;

            foreach (var word in entryWords)
            {
                if (headingWords.Contains(word)) continue; // already in heading
                if (word.Length < 3) continue;

                var frequency = allDescWords.Count(w => w.Equals(word, StringComparison.OrdinalIgnoreCase));
                // Prefer words that also appear in command segments
                var cmdBonus = cmdWords.Contains(word) ? -100 : 0;
                var score = frequency + cmdBonus;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestWord = word;
                }
            }

            // Also check command segments for distinctive words
            if (bestWord == null)
            {
                foreach (var word in cmdWordSets[i].words)
                {
                    var lower = word.ToLowerInvariant();
                    if (headingWords.Contains(lower)) continue;
                    if (lower.Length < 3) continue;
                    bestWord = lower;
                    break;
                }
            }

            if (bestWord != null)
            {
                var newHeading = Truncate($"{currentHeading} {bestWord}", 7);
                distinctiveWords[entries[i].Key] = newHeading;
            }
        }

        // Apply disambiguation and handle any remaining collisions
        foreach (var (command, newHeading) in distinctiveWords)
        {
            result[command] = newHeading;
        }

        // Final check: if still duplicates, append index
        var stillDuplicate = result
            .GroupBy(kv => kv.Value, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in stillDuplicate)
        {
            var items = group.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
            for (int i = 1; i < items.Count; i++)
            {
                var heading = items[i].Value;
                // Try adding more description context
                var desc = toolLookup.TryGetValue(items[i].Key, out var d) ? d : null;
                var extraWords = ExtractKeywords(desc)
                    .Where(w => !heading.Split(' ').Any(hw => hw.Equals(w, StringComparison.OrdinalIgnoreCase)))
                    .Where(w => w.Length >= 3)
                    .Take(2)
                    .ToList();

                if (extraWords.Count > 0)
                {
                    result[items[i].Key] = Truncate($"{heading} {string.Join(" ", extraWords)}", 7);
                }
                else
                {
                    // Last resort: append ordinal
                    result[items[i].Key] = Truncate($"{heading} {i + 1}", 7);
                }
            }
        }
    }

    private static List<string> ExtractKeywords(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return new List<string>();

        return description
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim(',', '.', ':', ';', '(', ')').ToLowerInvariant())
            .Where(w => !StopWords.Contains(w) && w.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string Pluralize(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase)) return phrase;

        var words = phrase.Split(' ');
        var lastWord = words[^1];

        // Simple pluralization rules
        if (lastWord.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
            lastWord.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            lastWord.EndsWith("sh", StringComparison.OrdinalIgnoreCase) ||
            lastWord.EndsWith("ch", StringComparison.OrdinalIgnoreCase))
        {
            words[^1] = lastWord + "es";
        }
        else if (lastWord.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
                 lastWord.Length > 1 &&
                 !"aeiou".Contains(lastWord[^2]))
        {
            words[^1] = lastWord[..^1] + "ies";
        }
        else
        {
            words[^1] = lastWord + "s";
        }

        return string.Join(" ", words);
    }

    private static string TitleCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(w =>
            char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "")));
    }

    /// <summary>
    /// Converts a heading to sentence case: first word capitalized, remaining words
    /// lowercase unless they are recognized proper nouns/acronyms.
    /// </summary>
    public static string ToSentenceCase(string heading)
    {
        if (string.IsNullOrWhiteSpace(heading)) return heading;

        var words = heading.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new string[words.Length];

        for (int i = 0; i < words.Length; i++)
        {
            if (ProperNounMap.TryGetValue(words[i], out var properForm))
            {
                result[i] = properForm;
            }
            else if (i == 0)
            {
                // First word: capitalize first letter, lowercase rest
                result[i] = char.ToUpperInvariant(words[i][0]) +
                            (words[i].Length > 1 ? words[i][1..].ToLowerInvariant() : "");
            }
            else
            {
                result[i] = words[i].ToLowerInvariant();
            }
        }

        return string.Join(" ", result);
    }

    private static string Truncate(string heading, int maxWords)
    {
        var words = heading.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= maxWords)
            return string.Join(" ", words);
        return string.Join(" ", words.Take(maxWords));
    }
}
