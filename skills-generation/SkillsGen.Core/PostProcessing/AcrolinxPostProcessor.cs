using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace SkillsGen.Core.PostProcessing;

public partial class AcrolinxPostProcessor
{
    private readonly ILogger<AcrolinxPostProcessor> _logger;
    private readonly List<TextReplacement> _replacements;
    private readonly Dictionary<string, string> _acronyms;

    private static readonly Dictionary<string, string> Contractions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["does not"] = "doesn't",
        ["do not"] = "don't",
        ["is not"] = "isn't",
        ["are not"] = "aren't",
        ["was not"] = "wasn't",
        ["were not"] = "weren't",
        ["has not"] = "hasn't",
        ["have not"] = "haven't",
        ["had not"] = "hadn't",
        ["will not"] = "won't",
        ["would not"] = "wouldn't",
        ["could not"] = "couldn't",
        ["should not"] = "shouldn't",
        ["can not"] = "can't",
        ["cannot"] = "can't",
        ["it is"] = "it's",
        ["that is"] = "that's",
    };

    public AcrolinxPostProcessor(string? replacementsJson, string? acronymsJson, ILogger<AcrolinxPostProcessor> logger)
    {
        _logger = logger;
        _replacements = LoadReplacements(replacementsJson);
        _acronyms = LoadAcronyms(acronymsJson);
    }

    public string Process(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;

        var result = content;

        // Apply static text replacements with word boundaries
        foreach (var replacement in _replacements)
        {
            var pattern = $@"\b{Regex.Escape(replacement.Parameter)}\b";
            result = Regex.Replace(result, pattern, replacement.NaturalLanguage, RegexOptions.None);
        }

        // Apply contractions
        foreach (var (phrase, contraction) in Contractions)
        {
            var pattern = $@"\b{Regex.Escape(phrase)}\b";
            result = Regex.Replace(result, pattern, contraction, RegexOptions.IgnoreCase);
        }

        // Expand acronyms on first use
        result = ExpandAcronymsFirstUse(result);

        // Normalize URLs - strip learn.microsoft.com prefix
        result = NormalizeUrls(result);

        return result;
    }

    private string ExpandAcronymsFirstUse(string content)
    {
        // Apply replacements in reverse order of match position to avoid index shifting
        var allMatches = new List<(int Index, int Length, string Replacement)>();

        foreach (var (acronym, expansion) in _acronyms)
        {
            var pattern = $@"\b{Regex.Escape(acronym)}\b";
            var match = Regex.Match(content, pattern);
            if (match.Success)
            {
                var expanded = $"{expansion} ({acronym})";
                allMatches.Add((match.Index, match.Length, expanded));
            }
        }

        // Sort by index descending so later replacements don't shift earlier indices
        foreach (var m in allMatches.OrderByDescending(x => x.Index))
        {
            content = content[..m.Index] + m.Replacement + content[(m.Index + m.Length)..];
        }

        return content;
    }

    private static string NormalizeUrls(string content)
    {
        // Remove https://learn.microsoft.com/en-us prefix
        content = Regex.Replace(content, @"https?://learn\.microsoft\.com/en-us(/[^\s\)""]+)", "$1");
        // Remove https://learn.microsoft.com prefix (without locale)
        content = Regex.Replace(content, @"https?://learn\.microsoft\.com(/[^\s\)""]+)", "$1");
        return content;
    }

    private static List<TextReplacement> LoadReplacements(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<TextReplacement>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static Dictionary<string, string> LoadAcronyms(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();

        try
        {
            var entries = JsonSerializer.Deserialize<List<AcronymEntry>>(json, JsonOptions) ?? [];
            var dict = new Dictionary<string, string>();
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.Acronym) && !string.IsNullOrEmpty(entry.Expansion))
                    dict[entry.Acronym] = entry.Expansion;
            }
            return dict;
        }
        catch
        {
            return new();
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private record TextReplacement(string Parameter, string NaturalLanguage);
    private record AcronymEntry(string Acronym, string Expansion, string? ContextPattern = null, string? ExpandedForm = null);
}
