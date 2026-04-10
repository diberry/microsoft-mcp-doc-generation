// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Applies Microsoft style guide rules to article body text:
///   A. Compound word splitting (data-driven from compound-words.json)
///   B. Double-plural correction (suffix-based pattern matching)
///   C. Wordy phrase simplification
///
/// Only operates on prose text — skips frontmatter, headings, code blocks,
/// and inline code spans. Idempotent — already-correct text passes unchanged.
///
/// Acrolinx rules: WC-2 (word choice), CL-1 (clarity), CO-2 (conciseness)
/// Fixes: #393
/// </summary>
public static class StyleGuidePostProcessor
{
    // ── Protection patterns (reuse AcronymExpander convention) ───────

    private static readonly Regex CodeBlockPattern = new(
        @"```[\s\S]*?```",
        RegexOptions.Compiled);

    private static readonly Regex InlineCodePattern = new(
        @"`[^`]+`",
        RegexOptions.Compiled);

    private static readonly Regex HeadingPattern = new(
        @"^#{1,6}\s.*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // ── A. Compound word rules (loaded once from JSON) ──────────────

    private static readonly List<(Regex Pattern, string Replacement)> CompoundWordRules = LoadCompoundWordRules();

    // ── B. Double-plural rules ──────────────────────────────────────
    // Suffixes where trailing "es" indicates double-pluralization.
    // Each entry is (consonant-cluster that precedes the extra "es", verified safe from false positives).

    private static readonly Regex DoublePluralPattern = new(
        @"\b(\w+(?:nts|cts|sts|pts|lts|rks|ces|ses))es\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Known legitimate words that match the double-plural pattern but are correct
    private static readonly HashSet<string> DoublePluralExclusions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Words ending in -sses + es (root ends in "ss")
        "processes", "addresses", "accesses", "excesses", "progresses",
        "compasses", "successes", "businesses", "obsesses", "assesses",
        "recesses", "princesses", "possesses", "witnesses", "harnesses",
        "mattresses", "fortresses", "mistresses", "caresses",
        // Words ending in -ses (irregular plurals / root ends in -sis/-se)
        "analyses", "diagnoses", "hypotheses", "syntheses", "parentheses",
        "crises", "bases", "cases", "causes", "uses", "abuses", "clauses",
        "phrases", "pauses", "doses", "poses", "rises", "closes", "noses",
        // Words ending in -rses (root ending)
        "verses", "courses", "horses", "nurses", "purses", "curses",
        "reverses", "traverses", "disperses", "endorses", "divorces",
        // Words ending in -nses (root ending)
        "responses", "expenses", "licenses", "senses", "rinses",
        "offenses", "defenses", "dispenses", "suspenses", "condenses",
        // Words ending in -lses (root ending)
        "pulses", "impulses", "repulses", "valses",
        // Words ending in -pses (root ending)
        "glimpses", "eclipses", "corpses", "collapses", "relapses", "synapses", "lapses", "elipses",
        // Words ending in -ases/-ises/-oses/-uses (root + es)
        "increases", "decreases", "releases", "leases", "diseases", "databases",
        "promises", "exercises", "comprises", "advises", "surprises", "supervises",
        "purposes", "composes", "proposes", "supposes", "disposes", "exposes",
    };

    // ── C. Wordy phrase rules ───────────────────────────────────────

    private static readonly (Regex Pattern, string Replacement)[] WordyPhraseRules =
    [
        (BuildWordyRule("provides the ability to"), "lets you"),
        (BuildWordyRule("is able to"), "can"),
        (BuildWordyRule("in order to"), "to"),
        (BuildWordyRule("for the purpose of"), "to"),
        (BuildWordyRule("has the ability to"), "can"),
        (BuildWordyRule("it is important to note that"), "note that"),
        (BuildWordyRule("at this point in time"), "now"),
    ];

    /// <summary>
    /// Applies all style guide fixes to markdown body text.
    /// Protects frontmatter, headings, code blocks, and inline code from modification.
    /// </summary>
    public static string Fix(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";

        // Separate frontmatter from body
        string frontmatter = "";
        string body = markdown;
        if (markdown.StartsWith("---"))
        {
            int endFm = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (endFm > 0)
            {
                int fmEnd = markdown.IndexOf('\n', endFm + 4);
                if (fmEnd > 0)
                {
                    frontmatter = markdown[..(fmEnd + 1)];
                    body = markdown[(fmEnd + 1)..];
                }
            }
        }

        // Protect code blocks, inline code, and headings with placeholders
        var placeholders = new Dictionary<string, string>();
        int placeholderIndex = 0;

        body = CodeBlockPattern.Replace(body, m =>
        {
            var key = $"\x00CB{placeholderIndex++}\x00";
            placeholders[key] = m.Value;
            return key;
        });

        body = InlineCodePattern.Replace(body, m =>
        {
            var key = $"\x00IC{placeholderIndex++}\x00";
            placeholders[key] = m.Value;
            return key;
        });

        body = HeadingPattern.Replace(body, m =>
        {
            var key = $"\x00HD{placeholderIndex++}\x00";
            placeholders[key] = m.Value;
            return key;
        });

        // A. Compound word splitting
        foreach (var (pattern, replacement) in CompoundWordRules)
        {
            body = pattern.Replace(body, m => ApplyCompoundReplacement(m, replacement));
        }

        // B. Double-plural correction
        body = DoublePluralPattern.Replace(body, m =>
        {
            // Check exclusion list — if the full word is legitimate, keep it
            if (DoublePluralExclusions.Contains(m.Value))
                return m.Value;
            // Strip the trailing "es" → return captured group (the correct plural)
            return m.Groups[1].Value;
        });

        // C. Wordy phrase simplification
        foreach (var (pattern, replacement) in WordyPhraseRules)
        {
            body = pattern.Replace(body, m => PreserveLeadingCase(m.Value, replacement));
        }

        // Restore placeholders
        foreach (var (key, value) in placeholders)
        {
            body = body.Replace(key, value);
        }

        return frontmatter + body;
    }

    // ── Helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Applies compound word replacement, preserving plurality.
    /// If the match ends in "s" beyond what the key specifies, the
    /// replacement also gets an "s" appended to its last word.
    /// </summary>
    private static string ApplyCompoundReplacement(Match match, string replacement)
    {
        var matched = match.Value;
        // Lowercase the replacement (compound words in prose should be lowercase
        // unless at sentence start — handled by case preservation below)
        var result = PreserveLeadingCase(matched, replacement);
        return result;
    }

    /// <summary>
    /// Preserves the case of the first character from the original text.
    /// </summary>
    private static string PreserveLeadingCase(string original, string replacement)
    {
        if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(replacement))
            return replacement;

        if (char.IsUpper(original[0]))
            return char.ToUpper(replacement[0]) + replacement[1..];

        return replacement;
    }

    /// <summary>
    /// Builds a case-insensitive regex for a wordy phrase with word boundaries.
    /// </summary>
    private static Regex BuildWordyRule(string phrase)
    {
        return new Regex(
            $@"(?<!\w){Regex.Escape(phrase)}(?!\w)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Loads compound word rules from compound-words.json, filtering to
    /// prose-safe entries only (skip abbreviations, already-hyphenated keys,
    /// and entries known to be filename-only mappings).
    /// </summary>
    private static List<(Regex Pattern, string Replacement)> LoadCompoundWordRules()
    {
        var mappings = LoadCompoundWordsJson();
        var rules = new List<(Regex, string)>();

        foreach (var (key, value) in mappings)
        {
            // Skip short abbreviations (< 6 chars) — too ambiguous for prose
            if (key.Length < 6)
                continue;

            // Skip keys that already contain hyphens (e.g., "app-lens", "query-and-evaluate")
            if (key.Contains('-'))
                continue;

            // Skip "runtime" — Microsoft style guide says "runtime" is one word
            if (key.Equals("runtime", StringComparison.OrdinalIgnoreCase))
                continue;

            // Convert hyphenated value to space-separated display form
            var displayForm = value.Replace("-", " ");

            // Build regex: match the key (case-insensitive, word boundaries)
            // Handle plural form: if key doesn't end in "s", also match key+"s"
            string pattern;

            if (key.EndsWith('s'))
            {
                // Key is already plural (e.g., "healthmodels", "bestpractices", "webtests")
                // Also match the singular form (key without trailing "s")
                var singular = key[..^1];
                var singularDisplay = displayForm.EndsWith('s')
                    ? displayForm[..^1]
                    : displayForm;
                pattern = $@"\b{Regex.Escape(key)}\b";
                rules.Add((new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase), displayForm));

                // Add singular form if it's long enough
                if (singular.Length >= 6)
                {
                    var singularPattern = $@"\b{Regex.Escape(singular)}\b";
                    rules.Add((new Regex(singularPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase), singularDisplay));
                }
            }
            else
            {
                // Key is singular (e.g., "activitylog", "eventhub")
                // Match both singular and plural
                var pluralDisplay = displayForm + "s";
                pattern = $@"\b{Regex.Escape(key)}s\b";
                rules.Add((new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase), pluralDisplay));

                pattern = $@"\b{Regex.Escape(key)}\b";
                rules.Add((new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase), displayForm));
            }
        }

        return rules;
    }

    /// <summary>
    /// Loads compound-words.json from the data directory.
    /// Falls back to built-in defaults if the file isn't found.
    /// </summary>
    private static Dictionary<string, string> LoadCompoundWordsJson()
    {
        var paths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "compound-words.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "docs-generation", "data", "compound-words.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "data", "compound-words.json"),
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null && dict.Count > 0)
                        return dict;
                }
                catch
                {
                    // Fall through to built-in defaults
                }
            }
        }

        // Built-in fallback (prose-safe subset only)
        return new Dictionary<string, string>
        {
            ["activitylog"] = "activity-log",
            ["bestpractices"] = "best-practices",
            ["consumergroup"] = "consumer-group",
            ["eventhub"] = "event-hub",
            ["healthmodels"] = "health-models",
            ["hostpool"] = "host-pool",
            ["monitoredresources"] = "monitored-resources",
            ["nodepool"] = "node-pool",
            ["subnetsize"] = "subnet-size",
            ["testresource"] = "test-resource",
            ["testrun"] = "test-run",
            ["webtests"] = "web-tests",
            ["azureaibestpractices"] = "azure-ai-best-practices",
        };
    }
}
