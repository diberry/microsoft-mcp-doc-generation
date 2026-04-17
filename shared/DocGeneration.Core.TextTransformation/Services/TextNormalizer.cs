using System.Text;
using System.Text.RegularExpressions;
using Azure.Mcp.TextTransformation.Models;

namespace Azure.Mcp.TextTransformation.Services;

/// <summary>
/// Provides text normalization and transformation services.
/// </summary>
public class TextNormalizer
{
    private readonly TransformationConfig _config;

    // Precompiled combined dictionary for O(1) lookups (NormalizeParameter per-word + ReplaceStaticText).
    // Built from Lexicon.Abbreviations at construction time. Case-insensitive.
    private readonly Dictionary<string, string> _combinedDict;

    // Precompiled single-pass alternation regex for ReplaceStaticText.
    // Uses lookaround boundaries matching TextCleanup behaviour.
    private readonly Regex? _replacerRegex;

    // Known acronym canonical forms for fast membership checks.
    private readonly HashSet<string> _acronymCanonicalSet;

    /// <summary>
    /// Initializes a new instance of the TextNormalizer class.
    /// </summary>
    /// <param name="config">The transformation configuration.</param>
    public TextNormalizer(TransformationConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        // Build combined dictionary from Abbreviations AND Parameters.Mappings.
        // In production (via TransformationConfigFactory), Abbreviations already contains
        // the merged nl-parameters + static-text-replacement data. But in unit tests,
        // Parameters.Mappings may be populated separately, so we include both.
        _combinedDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in _config.Parameters.Mappings)
        {
            if (!string.IsNullOrEmpty(mapping.Parameter))
                _combinedDict[mapping.Parameter] = mapping.Display;
        }
        foreach (var abbrev in _config.Lexicon.Abbreviations)
        {
            // Abbreviations override mappings when keys overlap (consistent with
            // TextCleanup where static-text-replacement entries override nl-parameters
            // for the same key — but in practice they don't overlap).
            _combinedDict[abbrev.Key] = abbrev.Value.Canonical;
        }

        // Build precompiled regex with lookaround boundaries (matches TextCleanup exactly).
        var keys = _combinedDict.Keys
            .Where(k => !string.IsNullOrEmpty(k))
            .OrderByDescending(k => k.Length)
            .ToArray();

        if (keys.Length > 0)
        {
            var patternParts = keys.Select(k => $"(?<![A-Za-z0-9_-]){Regex.Escape(k)}(?![A-Za-z0-9_-])");
            var pattern = string.Join("|", patternParts);
            _replacerRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }

        // Build canonical-form set for fast IsAcronym checks.
        _acronymCanonicalSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var acronym in _config.Lexicon.Acronyms.Values)
        {
            _acronymCanonicalSet.Add(acronym.Canonical);
        }
    }

    /// <summary>
    /// Normalizes a programmatic parameter name to natural language format.
    /// Checks identifier mappings first (resource type names), then generic mappings,
    /// then falls back to hyphen-splitting with per-word transformation.
    /// Matches legacy TextCleanup.NormalizeParameter behaviour exactly.
    /// </summary>
    public string NormalizeParameter(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName))
        {
            return "Unknown";
        }

        // Strip CLI-style "--" prefix before lookup
        if (parameterName.StartsWith("--"))
        {
            parameterName = parameterName.Substring(2);
        }

        // Check identifier mappings first (e.g., "database" -> "Database name")
        var identifier = _config.Parameters.Identifiers.FirstOrDefault(m =>
            m.Parameter.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
        if (identifier != null)
        {
            return identifier.Display;
        }

        // Check combined dict for full-name direct mapping
        if (_combinedDict.TryGetValue(parameterName, out var directMapping))
        {
            return directMapping;
        }

        // Fallback: split on hyphens and transform per-word (matches TextCleanup)
        return NormalizeHyphenatedName(parameterName);
    }

    /// <summary>
    /// Splits a hyphenated parameter name and applies per-word transformations.
    /// Matches TextCleanup.SplitAndTransformProgrammaticName + NormalizeParameter post-processing.
    /// </summary>
    private string NormalizeHyphenatedName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "Unknown";
        }

        var words = name.Split('-');

        if (words.Length == 0 || words[0].Length == 0)
        {
            return "Unknown";
        }

        // Transform each word: check acronyms, then combined dict, then capitalize
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            if (string.IsNullOrEmpty(word)) continue;

            // Check acronym table first
            var acronym = _config.Lexicon.Acronyms
                .FirstOrDefault(a => a.Key.Equals(word, StringComparison.OrdinalIgnoreCase));
            if (acronym.Key != null)
            {
                words[i] = acronym.Value.Canonical;
            }
            // Check combined dict for single-word mapping
            else if (_combinedDict.TryGetValue(word, out var nlValue))
            {
                words[i] = nlValue;
            }
            else
            {
                // Capitalize first letter
                words[i] = char.ToUpper(word[0]) + word.Substring(1);
            }
        }

        // Lowercase non-first non-acronym words (matches TextCleanup.NormalizeParameter)
        for (int i = 1; i < words.Length; i++)
        {
            if (!_acronymCanonicalSet.Contains(words[i]))
            {
                words[i] = words[i].ToLowerInvariant();
            }
        }

        var result = string.Join(" ", words);

        // Remove any periods to avoid "Resource. group." format
        result = result.Replace(".", "");

        return result;
    }

    /// <summary>
    /// Splits a programmatic name and applies acronym transformations.
    /// Uses camelCase splitting for PascalCase/camelCase identifiers.
    /// </summary>
    public string SplitAndTransformProgrammaticName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        // Split on capitals
        var words = SplitCamelCase(name);
        
        // Transform each word
        var transformed = words.Select(TransformWord).ToList();
        
        return string.Join(" ", transformed);
    }

    /// <summary>
    /// Transforms a single word (handles acronyms).
    /// </summary>
    private string TransformWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return string.Empty;
        }

        // Check if it's an acronym
        var acronym = _config.Lexicon.Acronyms
            .FirstOrDefault(a => a.Key.Equals(word, StringComparison.OrdinalIgnoreCase));
        
        if (acronym.Key != null)
        {
            return acronym.Value.Canonical;
        }

        // Lowercase unless it's all caps (likely an acronym we don't know about)
        if (word.All(char.IsUpper) && word.Length > 1)
        {
            return word;
        }

        return word.ToLowerInvariant();
    }

    /// <summary>
    /// Splits a camelCase or PascalCase string into words.
    /// </summary>
    private List<string> SplitCamelCase(string input)
    {
        var result = new List<string>();
        var currentWord = new StringBuilder();

        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (char.IsUpper(c) && currentWord.Length > 0)
            {
                // Check if this is start of a new word or part of acronym
                if (i + 1 < input.Length && char.IsLower(input[i + 1]))
                {
                    // New word starting
                    result.Add(currentWord.ToString());
                    currentWord.Clear();
                }
                else if (i > 0 && char.IsLower(input[i - 1]))
                {
                    // New word starting after lowercase
                    result.Add(currentWord.ToString());
                    currentWord.Clear();
                }
            }

            currentWord.Append(c);
        }

        if (currentWord.Length > 0)
        {
            result.Add(currentWord.ToString());
        }

        return result;
    }

    /// <summary>
    /// Replaces static text patterns in descriptions using precompiled regex.
    /// Uses lookaround word-boundary matching to avoid replacing inside longer tokens.
    /// Matches legacy TextCleanup.ReplaceStaticText behaviour exactly.
    /// </summary>
    public string ReplaceStaticText(string text)
    {
        if (string.IsNullOrEmpty(text) || _combinedDict.Count == 0)
        {
            return text;
        }

        if (_replacerRegex != null)
        {
            text = _replacerRegex.Replace(text, m =>
            {
                if (_combinedDict.TryGetValue(m.Value, out var replacement))
                {
                    return replacement ?? string.Empty;
                }
                return m.Value;
            });
        }
        else
        {
            // Fallback: ordered per-key regex replacement with boundary lookarounds
            var orderedKeys = _combinedDict.Keys
                .Where(k => !string.IsNullOrEmpty(k))
                .OrderByDescending(k => k.Length)
                .ToList();

            foreach (var key in orderedKeys)
            {
                var replacement = _combinedDict[key] ?? string.Empty;
                var pattern = $"(?<![A-Za-z0-9_-]){Regex.Escape(key)}(?![A-Za-z0-9_-])";
                text = Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase);
            }
        }

        return text;
    }

    /// <summary>
    /// Converts text to title case while preserving acronyms.
    /// </summary>
    public string ToTitleCase(string text, string context = "titleCase")
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            
            // Check if it's a stop word (not first or last word)
            if (i > 0 && i < words.Length - 1 && IsStopWord(word, context))
            {
                result.Add(word.ToLowerInvariant());
                continue;
            }

            // Check if it's an acronym that should be preserved
            var acronym = _config.Lexicon.Acronyms
                .FirstOrDefault(a => a.Key.Equals(word, StringComparison.OrdinalIgnoreCase));
            
            if (acronym.Key != null && acronym.Value.PreserveInTitleCase)
            {
                result.Add(acronym.Value.Canonical);
                continue;
            }

            // Capitalize first letter
            result.Add(CapitalizeFirst(word));
        }

        return string.Join(" ", result);
    }

    private bool IsStopWord(string word, string context)
    {
        if (!_config.Contexts.TryGetValue(context, out var rules))
        {
            return false;
        }

        if (!rules.Rules.TryGetValue("stopWords", out var stopWordRule))
        {
            return false;
        }

        return _config.Lexicon.StopWords.Contains(word.ToLowerInvariant());
    }

    private string CapitalizeFirst(string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return word;
        }

        return char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
    }

    private static readonly Regex BareExampleValuePattern = new(
        @"\(for example, ([^)`]+)\)",
        RegexOptions.Compiled);

    /// <summary>
    /// Wraps bare example values in backticks within "(for example, ...)" patterns.
    /// Idempotent - already-backticked values pass through unchanged.
    /// </summary>
    public string WrapExampleValues(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return BareExampleValuePattern.Replace(text, match =>
        {
            var values = match.Groups[1].Value;
            var parts = values.Split(", ");
            var backticked = parts.Select(v =>
            {
                var trimmed = v.Trim();
                var spaceIdx = trimmed.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    var value = trimmed.Substring(0, spaceIdx);
                    var explanation = trimmed.Substring(spaceIdx);
                    return $"`{value}`{explanation}";
                }
                return $"`{trimmed}`";
            });
            return $"(for example, {string.Join(", ", backticked)})";
        });
    }

    /// <summary>
    /// Cleans AI-generated text by replacing smart quotes with straight quotes
    /// and HTML entities with plain characters.
    /// </summary>
    public string CleanAIGeneratedText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Replace smart/curly quotes with straight quotes
        text = text.Replace('\u2018', '\'');  // left single
        text = text.Replace('\u2019', '\'');  // right single
        text = text.Replace('\u201C', '"');   // left double
        text = text.Replace('\u201D', '"');   // right double

        // Replace HTML entities with their plain character equivalents
        text = text.Replace("&quot;", "\"");
        text = text.Replace("&#34;", "\"");
        text = text.Replace("&apos;", "'");
        text = text.Replace("&#39;", "'");
        text = text.Replace("&amp;", "&");
        text = text.Replace("&lt;", "<");
        text = text.Replace("&gt;", ">");

        return text;
    }

    /// <summary>
    /// Ensures text ends with a period, adding one if missing.
    /// Matches legacy TextCleanup.EnsureEndsPeriod behaviour exactly,
    /// including trailing-quote awareness.
    /// </summary>
    public string EnsureEndsPeriod(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        text = text.Trim();

        // Skip if already ends with punctuation
        if (text.EndsWith(".") || text.EndsWith("?") || text.EndsWith("!"))
        {
            return text;
        }

        // Check for punctuation before trailing closing quotes
        var lastChar = text[^1];
        if (lastChar == '\'' || lastChar == '"' || lastChar == '`')
        {
            var i = text.Length - 1;
            while (i > 0 && (text[i] == '\'' || text[i] == '"' || text[i] == '`'))
                i--;
            if (i >= 0 && (text[i] == '.' || text[i] == '?' || text[i] == '!'))
                return text;
        }

        return text + ".";
    }
}
