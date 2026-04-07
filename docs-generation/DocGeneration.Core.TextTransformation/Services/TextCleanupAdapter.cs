using System.Text.Json;
using System.Text.RegularExpressions;

namespace Azure.Mcp.TextTransformation.Services;

/// <summary>
/// Legacy-compatibility façade that preserves the exact behavior of
/// NaturalLanguageGenerator.TextCleanup while living in the TextTransformation project.
///
/// Phase 1 of Issue #352: callers can swap to this adapter with zero behavior drift.
/// Phase 2 will migrate callers. Phase 3 will remove NaturalLanguage entirely.
/// </summary>
public class TextCleanupAdapter
{
    private Dictionary<string, string>? _mappedParametersDict;
    private Dictionary<string, string>? _parameterIdentifiersDict;
    private Regex? _replacerRegex;

    /// <summary>
    /// Returns true after LoadFiles has been called successfully at least once.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Loads NL-format parameter files (nl-parameters.json, static-text-replacement.json)
    /// and builds the replacement regex. Mirrors TextCleanup.LoadFiles behavior exactly.
    /// </summary>
    public bool LoadFiles(List<string> requiredFiles)
    {
        if (requiredFiles == null || requiredFiles.Count == 0)
        {
            return false;
        }

        try
        {
            string? nlParametersPath = null;
            string? textReplacerPath = null;

            foreach (var file in requiredFiles)
            {
                if (file.IndexOf("nl-parameters.json", StringComparison.OrdinalIgnoreCase) >= 0)
                    nlParametersPath = file;
                if (file.IndexOf("static-text-replacement.json", StringComparison.OrdinalIgnoreCase) >= 0)
                    textReplacerPath = file;
            }

            var combinedParameters = new List<MappedParameterDto>();

            if (File.Exists(nlParametersPath))
            {
                var entries = JsonSerializer.Deserialize<List<MappedParameterDto>>(File.ReadAllText(nlParametersPath));
                if (entries != null) combinedParameters.AddRange(entries);
            }

            if (File.Exists(textReplacerPath))
            {
                var entries = JsonSerializer.Deserialize<List<MappedParameterDto>>(File.ReadAllText(textReplacerPath));
                if (entries != null) combinedParameters.AddRange(entries);
            }

            // Deduplicate by Parameter key (case-insensitive, first wins)
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mp in combinedParameters)
            {
                if (!string.IsNullOrEmpty(mp.Parameter) && !dict.ContainsKey(mp.Parameter))
                {
                    dict[mp.Parameter] = mp.NaturalLanguage ?? string.Empty;
                }
            }
            _mappedParametersDict = dict;

            // Build precompiled alternation regex (longer keys first, word-boundary lookarounds)
            var keys = dict.Keys
                .Where(k => !string.IsNullOrEmpty(k))
                .OrderByDescending(k => k.Length)
                .ToArray();

            if (keys.Length > 0)
            {
                var patternParts = keys.Select(k => $"(?<![A-Za-z0-9_-]){Regex.Escape(k)}(?![A-Za-z0-9_-])");
                var pattern = string.Join("|", patternParts);
                _replacerRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }

            // Load resource identifier mappings (auto-discovered from nl-parameters.json directory)
            _parameterIdentifiersDict = null;
            if (!string.IsNullOrEmpty(nlParametersPath))
            {
                var dir = Path.GetDirectoryName(nlParametersPath);
                if (dir != null)
                {
                    var identifiersPath = Path.Combine(dir, "nl-parameter-identifiers.json");
                    if (File.Exists(identifiersPath))
                    {
                        var identifiers = JsonSerializer.Deserialize<List<MappedParameterDto>>(File.ReadAllText(identifiersPath));
                        if (identifiers != null && identifiers.Count > 0)
                        {
                            var idDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var mp in identifiers)
                            {
                                if (!string.IsNullOrEmpty(mp.Parameter))
                                    idDict[mp.Parameter] = mp.NaturalLanguage ?? string.Empty;
                            }
                            _parameterIdentifiersDict = idDict;
                        }
                    }
                }
            }

            IsInitialized = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Replaces static text patterns using the precompiled regex.
    /// Mirrors TextCleanup.ReplaceStaticText behavior exactly.
    /// </summary>
    public string ReplaceStaticText(string text)
    {
        if (string.IsNullOrEmpty(text) || _mappedParametersDict == null || _mappedParametersDict.Count == 0)
        {
            return text;
        }

        if (_replacerRegex != null)
        {
            text = _replacerRegex.Replace(text, m =>
            {
                if (_mappedParametersDict.TryGetValue(m.Value, out var replacement))
                    return replacement ?? string.Empty;
                return m.Value;
            });
        }

        return text;
    }

    /// <summary>
    /// Normalizes a hyphenated programmatic parameter name to natural language.
    /// Mirrors TextCleanup.NormalizeParameter exactly: hyphen-split, acronym transform,
    /// first-word capitalization, preserves all words.
    /// </summary>
    public static string NormalizeParameter(string programmaticName)
    {
        if (string.IsNullOrEmpty(programmaticName))
            return "Unknown";

        if (programmaticName.StartsWith("--"))
            programmaticName = programmaticName.Substring(2);

        // Split on hyphens
        var words = programmaticName.Split('-');
        if (words.Length == 0 || words[0].Length == 0)
            return "Unknown";

        // Transform each word: acronyms first, then capitalize first letter
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            if (string.IsNullOrEmpty(word)) continue;

            word = TransformAcronym(word);

            // Only first word capitalized, rest lowercase (unless acronym)
            if (i > 0 && !IsKnownAcronym(word))
            {
                words[i] = word.ToLowerInvariant();
            }
            else if (i == 0 && !IsKnownAcronym(word))
            {
                words[i] = char.ToUpper(word[0]) + word.Substring(1);
            }
            else
            {
                words[i] = word;
            }
        }

        var result = string.Join(" ", words).Replace(".", "");
        return result;
    }

    /// <summary>
    /// Ensures text ends with a period, adding one if missing.
    /// Mirrors TextCleanup.EnsureEndsPeriod exactly, including trailing-quote handling.
    /// </summary>
    public static string EnsureEndsPeriod(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        text = text.Trim();

        if (text.EndsWith(".") || text.EndsWith("?") || text.EndsWith("!"))
            return text;

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

    private static readonly Regex BareExampleValuePattern = new(
        @"\(for example, ([^)`]+)\)",
        RegexOptions.Compiled);

    /// <summary>
    /// Wraps bare example values in backticks within "(for example, ...)" patterns.
    /// Mirrors TextCleanup.WrapExampleValues exactly.
    /// </summary>
    public static string WrapExampleValues(string text)
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
    /// Cleans AI-generated text by replacing smart quotes and HTML entities.
    /// Mirrors TextCleanup.CleanAIGeneratedText exactly.
    /// </summary>
    public static string CleanAIGeneratedText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        text = text.Replace('\u2018', '\'');
        text = text.Replace('\u2019', '\'');
        text = text.Replace('\u201C', '"');
        text = text.Replace('\u201D', '"');

        text = text.Replace("&quot;", "\"");
        text = text.Replace("&#34;", "\"");
        text = text.Replace("&apos;", "'");
        text = text.Replace("&#39;", "'");
        text = text.Replace("&amp;", "&");
        text = text.Replace("&lt;", "<");
        text = text.Replace("&gt;", ">");

        return text;
    }

    // ─────────────────────────────────────────────
    // Private helpers — exact copy of NL's acronym logic
    // ─────────────────────────────────────────────

    private static string TransformAcronym(string word)
    {
        return word.ToLowerInvariant() switch
        {
            "id" => "ID",
            "ids" => "IDs",
            "uri" => "URI",
            "url" => "URL",
            "urls" => "URLs",
            "ai" => "AI",
            "api" => "API",
            "apis" => "APIs",
            "cpu" => "CPU",
            "gpu" => "GPU",
            "ip" => "IP",
            "sql" => "SQL",
            "vm" => "VM",
            "vms" => "VMs",
            "dns" => "DNS",
            "sku" => "SKU",
            "skus" => "SKUs",
            "tls" => "TLS",
            "ssl" => "SSL",
            "http" => "HTTP",
            "https" => "HTTPS",
            "json" => "JSON",
            "xml" => "XML",
            "yaml" => "YAML",
            "oauth" => "OAuth",
            "etag" => "ETag",
            "cdn" => "CDN",
            "rg" => "Resource group",
            _ => word
        };
    }

    private static readonly string[] KnownAcronyms =
    {
        "ID", "IDs", "URI", "URL", "URLs", "AI", "API", "APIs",
        "CPU", "GPU", "IP", "SQL", "VM", "VMs", "DNS", "SKU", "SKUs",
        "TLS", "SSL", "HTTP", "HTTPS", "JSON", "XML", "YAML",
        "OAuth", "ETag", "CDN", "Resource group"
    };

    private static bool IsKnownAcronym(string word)
    {
        return KnownAcronyms.Contains(word);
    }

    /// <summary>
    /// Internal DTO for deserializing NL-format JSON parameter files.
    /// </summary>
    private class MappedParameterDto
    {
        public string Parameter { get; set; } = string.Empty;
        public string NaturalLanguage { get; set; } = string.Empty;
    }
}
