using System.Text.Json;
using System.Text.RegularExpressions;
using Shared;

namespace NaturalLanguageGenerator;


public static class TextCleanup
{
    private static string? nlParametersPath = null;

    private static string? textReplacerParametersPath = null;

    public static string? TextReplacerParametersFilePath => textReplacerParametersPath;
    public static string? ParametersFilePath => nlParametersPath;

    public static MappedParameter[]? mappedParameters { get; private set; }

    private static Dictionary<string, string>? mappedParametersDict;
    // Separate dictionary for resource identifier parameter names (Issue #270).
    // Used ONLY in NormalizeParameter full-name lookup, NOT in ReplaceStaticText.
    private static Dictionary<string, string>? parameterIdentifiersDict;
    // Precompiled regex for multi-key replacement (constructed in LoadFiles)
    private static Regex? replacerRegex;

    public static bool LoadFiles(List<string> RequiredFiles)
    {
        try
        {
            if (RequiredFiles == null || RequiredFiles.Count == 0)
            {
                LogFileHelper.WriteDebug("RequiredFiles list is null or empty. Returning null.");
                return false;
            }

            List<MappedParameter> combinedParameters = new();

            for (int i = 0; i < RequiredFiles.Count; i++)
            {
                var file = RequiredFiles[i];

                if (file.IndexOf("nl-parameters.json", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    nlParametersPath = file;
                }
                if (file.IndexOf("static-text-replacement.json", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    textReplacerParametersPath = file;
                }
            }

            if (File.Exists(nlParametersPath))
            {
                var nlJsonArray = JsonSerializer.Deserialize<List<MappedParameter>>(File.ReadAllText(nlParametersPath));
                if (nlJsonArray != null)
                {
                    combinedParameters.AddRange(nlJsonArray);
                }
            }
            else
            {
                LogFileHelper.WriteDebug($"nl-parameters.json file not found at '{nlParametersPath}'.");
            }

            if (File.Exists(textReplacerParametersPath))
            {
                var textReplaceJsonArray = JsonSerializer.Deserialize<List<MappedParameter>>(File.ReadAllText(textReplacerParametersPath));
                if (textReplaceJsonArray != null)
                {
                    combinedParameters.AddRange(textReplaceJsonArray);
                }
            }
            else
            {
                LogFileHelper.WriteDebug($"static-text-replacement.json file not found at '{textReplacerParametersPath}'.");
            }

            // Combine and deduplicate parameters based on the 'Parameter' property
            mappedParameters = combinedParameters
                .GroupBy(p => p.Parameter, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToArray();

            // Build a case-insensitive dictionary for O(1) lookups
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mp in mappedParameters)
            {
                if (!string.IsNullOrEmpty(mp.Parameter))
                {
                    dict[mp.Parameter] = mp.NaturalLanguage ?? string.Empty;
                }
            }
            mappedParametersDict = dict;

            // Pre-build a single compiled alternation regex (longer keys first) for faster replacements.
            // Wrap each key with lookarounds so it matches only as a full word (not inside another token).
            var keys = dict.Keys
                .Where(k => !string.IsNullOrEmpty(k))
                .OrderByDescending(k => k.Length)
                .ToArray();

            if (keys.Length > 0)
            {
                var patternParts = keys.Select(k => $"(?<![A-Za-z0-9_-]){Regex.Escape(k)}(?![A-Za-z0-9_-])");
                var pattern = string.Join("|", patternParts);
                replacerRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
            else
            {
                replacerRegex = null;
            }


            // Load resource identifier mappings from nl-parameter-identifiers.json (Issue #270).
            // Auto-discovered from the same directory as nl-parameters.json.
            parameterIdentifiersDict = null;
            if (!string.IsNullOrEmpty(nlParametersPath))
            {
                var dir = Path.GetDirectoryName(nlParametersPath);
                if (dir != null)
                {
                    var identifiersPath = Path.Combine(dir, "nl-parameter-identifiers.json");
                    if (File.Exists(identifiersPath))
                    {
                        var identifiers = JsonSerializer.Deserialize<List<MappedParameter>>(File.ReadAllText(identifiersPath));
                        if (identifiers != null && identifiers.Count > 0)
                        {
                            var idDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var mp in identifiers)
                            {
                                if (!string.IsNullOrEmpty(mp.Parameter))
                                {
                                    idDict[mp.Parameter] = mp.NaturalLanguage ?? string.Empty;
                                }
                            }
                            parameterIdentifiersDict = idDict;
                        }
                    }
                }
            }
            return true;

        }
        catch (Exception ex)
        {
            LogFileHelper.WriteDebug($"Error loading parameters: {ex.Message}");
            return false;
        }
    }

    private static string[] SplitAndTransformProgrammaticName(string programmaticName)
    {
        if (string.IsNullOrEmpty(programmaticName))
        {
            LogFileHelper.WriteDebug("Empty programmatic name provided to SplitAndTransformProgrammaticName");
            return new string[] { "Unknown" };
        }
        
        try
        {
            // Handle CLI-style parameters that start with "--"
            if (programmaticName.StartsWith("--"))
            {
                programmaticName = programmaticName.Substring(2);
            }
            
            // Split the programmatic name into words
            var words = programmaticName.Split('-');
            
            if (words.Length == 0 || words[0].Length == 0)
            {
                LogFileHelper.WriteDebug($"Invalid programmatic name format: '{programmaticName}'");
                return new string[] { "Unknown" };
            }

            // Transform each word: capitalize first letter, replace acronyms
            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i];
                if (string.IsNullOrEmpty(word)) continue;
                
                // Handle known abbreviations and acronyms
                word = TransformAcronyms(word);
                
                // Check if the word is in our replacement dictionary
                if (mappedParametersDict != null && mappedParametersDict.TryGetValue(word, out var naturalLanguageValue))
                {
                    words[i] = naturalLanguageValue;
                }
                else
                {
                    // Capitalize the first letter
                    words[i] = char.ToUpper(word[0]) + word.Substring(1);
                }
            }

            return words;
        }
        catch (Exception ex)
        {
            LogFileHelper.WriteDebug($"Error processing programmatic name '{programmaticName}': {ex.Message}");
            return new string[] { "Unknown" };
        }
    }
    
    /// <summary>
    /// Transforms common acronyms and abbreviations to their proper form
    /// </summary>
    private static string TransformAcronyms(string word)
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

    /// <summary>
    /// Normalizes a programmatic parameter name to natural language format.
    /// IMPORTANT: This method preserves ALL words in the parameter name, including type qualifiers
    /// like "name". For example, "resource-group-name" becomes "Resource group name" (NOT "Resource group").
    /// </summary>
    /// <param name="programmaticName">The programmatic parameter name (e.g., "resource-group-name")</param>
    /// <returns>Natural language parameter name with all words preserved (e.g., "Resource group name")</returns>
    public static string NormalizeParameter(string programmaticName)
    {
        if (string.IsNullOrEmpty(programmaticName))
        {
            LogFileHelper.WriteDebug("Empty parameter name provided to NormalizeParameter");
            return "Unknown";
        }

        // Handle CLI-style parameters that start with "--"
        if (programmaticName.StartsWith("--"))
        {
            programmaticName = programmaticName.Substring(2);
        }

        // Check resource identifier mappings first (Issue #270).
        // These map bare resource type names (e.g., "database") to "{ResourceType} name" format.
        if (parameterIdentifiersDict != null && parameterIdentifiersDict.TryGetValue(programmaticName, out var identifierName))
        {
            LogFileHelper.WriteDebug($"Found resource identifier name for '{programmaticName}': {identifierName}");
            return identifierName;
        }

        // Check if we have a direct mapping in our dictionary
        if (mappedParametersDict != null && mappedParametersDict.TryGetValue(programmaticName, out var naturalLanguageName))
        {
            LogFileHelper.WriteDebug($"Found natural language name for '{programmaticName}': {naturalLanguageName}");
            return naturalLanguageName;
        }

        // Word isn't in list - break it apart and fix it
        var words = SplitAndTransformProgrammaticName(programmaticName);

        // IMPORTANT: Process ALL words - do NOT remove any suffix words like "name", "id", etc.
        // These qualifiers are essential to the parameter's meaning.
        for (int i = 0; i < words.Length; i++)
        {
            // Don't call ReplaceStaticText here as it adds periods to each word
            // Just handle capitalization directly
            
            // Only keep first word capitalized, lowercase the rest
            // Skip known acronyms that should remain uppercase
            if (i > 0 && !IsAcronym(words[i]))
            {
                words[i] = words[i].ToLowerInvariant();
            }
        }

        // Join ALL words with spaces, not periods
        // DO NOT skip or remove any words - all parts of the parameter name are significant
        var result = string.Join(" ", words);
        
        // Remove any periods in the output to avoid "Resource. group." format
        result = result.Replace(".", "");
        
        LogFileHelper.WriteDebug($"Converted '{programmaticName}' to natural language: {result}");

        return result;
    }
    
    // Helper method to check if a word is a known acronym that should remain uppercase
    private static bool IsAcronym(string word)
    {
        string[] knownAcronyms = { "ID", "IDs", "URI", "URL", "URLs", "AI", "API", "APIs", "CPU", "GPU", "IP", "SQL", "VM", "VMs", "DNS", "SKU", "SKUs", "TLS", "SSL", "HTTP", "HTTPS", "JSON", "XML", "YAML", "OAuth", "ETag", "CDN" };
        return knownAcronyms.Contains(word);
    }
    public static string ReplaceStaticText(string text)
    {
        if (string.IsNullOrEmpty(text) || mappedParametersDict == null || mappedParametersDict.Count == 0)
        {
            return text;
        }

        // If we have a precompiled regex, do a single-pass replacement with a MatchEvaluator.
        if (replacerRegex != null)
        {
            text = replacerRegex.Replace(text, m =>
            {
                // mappedParametersDict is case-insensitive; match.Value is the matched key
                if (mappedParametersDict.TryGetValue(m.Value, out var replacement))
                {
                    return replacement ?? string.Empty;
                }
                return m.Value;
            });
        }
        else
        {
            // Fallback: ordered per-key regex replacement (less efficient) with boundary lookarounds
            var orderedKeys = mappedParametersDict.Keys
                .Where(k => !string.IsNullOrEmpty(k))
                .OrderByDescending(k => k.Length)
                .ToList();

            foreach (var key in orderedKeys)
            {
                var replacement = mappedParametersDict[key] ?? string.Empty;
                var pattern = $"(?<![A-Za-z0-9_-]){Regex.Escape(key)}(?![A-Za-z0-9_-])";
                text = Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase);
            }
        }

        // Don't automatically add period - caller will use EnsureEndsPeriod if needed
        return text;
    }
    
    /// <summary>
    /// Ensures text ends with a period, adding one if missing. Should only be used for parameter descriptions.
    /// </summary>
    public static string EnsureEndsPeriod(string text)
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

    private static readonly Regex BareExampleValuePattern = new(
        @"\(for example, ([^)`]+)\)",
        RegexOptions.Compiled);

    /// <summary>
    /// Wraps bare example values in backticks within "(for example, ...)" patterns.
    /// Idempotent — already-backticked values pass through unchanged.
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
                    // Value with explanation text (e.g., "PT1H for 1 hour")
                    // — only backtick the value token, keep explanation as-is
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
    /// Cleans AI-generated text by replacing smart quotes with straight quotes and HTML entities with plain characters.
    /// This is a safety net to fix issues that the AI model might generate despite prompt instructions.
    /// </summary>
    /// <param name="text">The text to clean (typically AI-generated example prompts)</param>
    /// <returns>Cleaned text with only straight quotes and plain characters</returns>
    public static string CleanAIGeneratedText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Replace smart/curly quotes with straight quotes
        // Unicode characters for smart quotes:
        // U+2018: ' (left single quotation mark)
        // U+2019: ' (right single quotation mark, also used as apostrophe)
        // U+201C: " (left double quotation mark)
        // U+201D: " (right double quotation mark)
        text = text.Replace('\u2018', '\'');  // ' → '
        text = text.Replace('\u2019', '\'');  // ' → '
        text = text.Replace('\u201C', '"');   // " → "
        text = text.Replace('\u201D', '"');   // " → "

        // Replace HTML entities with their plain character equivalents
        text = text.Replace("&quot;", "\"");  // &quot; → "
        text = text.Replace("&#34;", "\"");   // &#34; → "
        text = text.Replace("&apos;", "'");   // &apos; → '
        text = text.Replace("&#39;", "'");    // &#39; → '
        text = text.Replace("&amp;", "&");    // &amp; → &
        text = text.Replace("&lt;", "<");     // &lt; → <
        text = text.Replace("&gt;", ">");     // &gt; → >

        return text;
    }
}