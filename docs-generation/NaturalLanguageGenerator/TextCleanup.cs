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
    // Precompiled regex for multi-key replacement (constructed in LoadFiles)
    private static Regex? replacerRegex;

    public static bool LoadFiles(List<string> RequiredFiles)
    {
        try
        {
            if (RequiredFiles == null || RequiredFiles.Count == 0)
            {
                Console.WriteLine("Warning: RequiredFiles list is null or empty. Returning null.");
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
                Console.WriteLine($"Warning: nl-parameters.json file not found at '{nlParametersPath}'.");
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
                Console.WriteLine($"Warning: static-text-replacement.json file not found at '{textReplacerParametersPath}'.");
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

            return true;

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading parameters: {ex.Message}");
            return false;
        }
    }

    private static string[] SplitAndTransformProgrammaticName(string programmaticName)
    {
        if (string.IsNullOrEmpty(programmaticName))
        {
            Console.WriteLine($"Warning: Empty programmatic name provided to SplitAndTransformProgrammaticName");
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
                Console.WriteLine($"Warning: Invalid programmatic name format: '{programmaticName}'");
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
            Console.WriteLine($"Error processing programmatic name '{programmaticName}': {ex.Message}");
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
            "cdn" => "CDN",
            "rg" => "Resource group",
            _ => word
        };
    }

    public static string NormalizeParameter(string programmaticName)
    {
        if (string.IsNullOrEmpty(programmaticName))
        {
            Console.WriteLine("Warning: Empty parameter name provided to NormalizeParameter");
            return "Unknown";
        }

        // Handle CLI-style parameters that start with "--"
        if (programmaticName.StartsWith("--"))
        {
            programmaticName = programmaticName.Substring(2);
        }

        // Check if we have a direct mapping in our dictionary
        if (mappedParametersDict != null && mappedParametersDict.TryGetValue(programmaticName, out var naturalLanguageName))
        {
            Console.WriteLine($"Found natural language name for '{programmaticName}': {naturalLanguageName}");
            return naturalLanguageName;
        }

        // Word isn't in list - break it apart and fix it
        var words = SplitAndTransformProgrammaticName(programmaticName);

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

        // Join words with spaces, not periods
        var result = string.Join(" ", words);
        
        // Remove any periods in the output to avoid "Resource. group." format
        result = result.Replace(".", "");
        
        Console.WriteLine($"Converted '{programmaticName}' to natural language: {result}");

        return result;
    }
    
    // Helper method to check if a word is a known acronym that should remain uppercase
    private static bool IsAcronym(string word)
    {
        string[] knownAcronyms = { "ID", "IDs", "URI", "URL", "URLs", "AI", "API", "APIs", "CPU", "GPU", "IP", "SQL", "VM", "VMs", "DNS", "SKU", "SKUs", "TLS", "SSL", "HTTP", "HTTPS", "JSON", "XML", "YAML", "OAuth", "CDN" };
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
        
        return text + ".";
    }
}
