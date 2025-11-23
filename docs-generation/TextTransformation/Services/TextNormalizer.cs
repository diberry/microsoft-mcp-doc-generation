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

    /// <summary>
    /// Initializes a new instance of the TextNormalizer class.
    /// </summary>
    /// <param name="config">The transformation configuration.</param>
    public TextNormalizer(TransformationConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Normalizes a parameter name to natural language.
    /// </summary>
    public string NormalizeParameter(string parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            return string.Empty;
        }

        // Check for direct mapping
        var mapping = _config.Parameters.Mappings.FirstOrDefault(m => 
            m.Parameter.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
        if (mapping != null)
        {
            return mapping.Display;
        }

        // Split camelCase/PascalCase and transform
        return SplitAndTransformProgrammaticName(parameterName);
    }

    /// <summary>
    /// Splits a programmatic name and applies acronym transformations.
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
    /// Replaces static text patterns in descriptions.
    /// </summary>
    public string ReplaceStaticText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var result = text;

        // Replace abbreviations
        foreach (var abbrev in _config.Lexicon.Abbreviations)
        {
            result = Regex.Replace(result, 
                $@"\b{Regex.Escape(abbrev.Key)}\b", 
                abbrev.Value.Canonical, 
                RegexOptions.IgnoreCase);
        }

        return result;
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

    /// <summary>
    /// Ensures text ends with a period.
    /// </summary>
    public string EnsureEndsPeriod(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = text.TrimEnd();
        if (!text.EndsWith('.') && !text.EndsWith('!') && !text.EndsWith('?'))
        {
            return text + ".";
        }

        return text;
    }
}
