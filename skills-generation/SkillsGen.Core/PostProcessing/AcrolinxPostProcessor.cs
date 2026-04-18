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

    // Introductory adverbs/phrases that need a comma when starting a sentence
    private static readonly string[] IntroductoryWords =
    [
        "However",
        "Therefore",
        "Additionally",
        "Furthermore",
        "Moreover",
        "Meanwhile",
        "Otherwise",
        "Alternatively",
        "Consequently",
        "Specifically",
        "Similarly",
        "Typically",
    ];

    private static readonly string[] IntroductoryPhrases =
    [
        "For example",
        "By default",
        "In addition",
        "In general",
        "In particular",
        "For instance",
        "As a result",
        "On the other hand",
    ];

    // Conjunctions for sentence splitting
    private static readonly string[] SplitConjunctions =
    [
        " and ",
        " but ",
        " or ",
        " which ",
        " that ",
    ];

    public AcrolinxPostProcessor(string? replacementsJson, string? acronymsJson, ILogger<AcrolinxPostProcessor> logger)
    {
        _logger = logger;
        _replacements = LoadReplacements(replacementsJson);
        _acronyms = LoadAcronyms(acronymsJson);
    }

    public string Process(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;

        // Separate frontmatter from body — only post-process the body
        var (frontmatter, body) = SplitFrontmatter(content);

        // Apply key static replacements to frontmatter description too
        var processedFrontmatter = frontmatter;
        foreach (var replacement in _replacements)
        {
            var pattern = $@"\b{Regex.Escape(replacement.Parameter)}\b";
            processedFrontmatter = Regex.Replace(processedFrontmatter, pattern, replacement.NaturalLanguage, RegexOptions.None);
        }

        var result = body;

        // 1. Wrap bare skill names FIRST — backticked content is then protected
        result = WrapBareSkillNames(result);

        // 2. Static text replacements (protect backtick spans)
        result = ApplyWithBacktickProtection(result, text =>
        {
            foreach (var replacement in _replacements)
            {
                var pattern = $@"\b{Regex.Escape(replacement.Parameter)}\b";
                text = Regex.Replace(text, pattern, replacement.NaturalLanguage, RegexOptions.None);
            }
            return text;
        });

        // 3. Contractions (protect backtick spans)
        result = ApplyWithBacktickProtection(result, text =>
        {
            foreach (var (phrase, contraction) in Contractions)
            {
                var pattern = $@"\b{Regex.Escape(phrase)}\b";
                text = Regex.Replace(text, pattern, contraction, RegexOptions.IgnoreCase);
            }
            return text;
        });

        // 4. Expand acronyms on first use — after all other text is settled
        result = ExpandAcronymsFirstUse(result);

        // 5. Remove duplicate acronym expansions introduced by expansion step
        result = Regex.Replace(result, @"(\w[\w\s]+?)\s*\(\1\s*\((\w+)\)\)", "$1 ($2)", RegexOptions.IgnoreCase);

        // 6. Normalize URLs - strip learn.microsoft.com prefix
        result = NormalizeUrls(result);

        // 7. Rewrite goal-before-action patterns ("Run X to Y" → "To Y, run X")
        result = RewriteGoalBeforeAction(result);

        // 8. Wrap technical API terms in backticks
        result = WrapTechnicalTerms(result);

        // Add commas after introductory phrases
        result = AddIntroductoryCommas(result);

        // Remove bold label colons ("**Label:** text" → "**Label** text")
        result = RemoveBoldLabelColons(result);

        // Split long sentences
        result = SplitLongSentences(result);

        // Final cleanup: remove consecutive duplicate sentences
        result = RemoveConsecutiveDuplicateSentences(result);

        return processedFrontmatter + result;
    }

    /// <summary>
    /// Applies lightweight text-level transformations (static replacements and contractions)
    /// to a plain-text string. Safe for trigger prompts and other non-markdown content.
    /// Does NOT apply frontmatter splitting, skill name wrapping, URL normalization,
    /// acronym expansion, or sentence splitting.
    /// </summary>
    public string ProcessText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = text;

        // Static text replacements
        foreach (var replacement in _replacements)
        {
            var pattern = $@"\b{Regex.Escape(replacement.Parameter)}\b";
            result = Regex.Replace(result, pattern, replacement.NaturalLanguage, RegexOptions.None);
        }

        // Contractions
        foreach (var (phrase, contraction) in Contractions)
        {
            var pattern = $@"\b{Regex.Escape(phrase)}\b";
            result = Regex.Replace(result, pattern, contraction, RegexOptions.IgnoreCase);
        }

        return result;
    }

    /// <summary>
    /// Wraps known technical/API terms in backticks so Acrolinx treats them as code.
    /// Only wraps terms NOT already inside backtick spans.
    /// </summary>
    private static string WrapTechnicalTerms(string content)
    {
        var terms = new[]
        {
            "DefaultAzureCredential", "CopilotClient", "createSession", "sendAndWait",
            "eventhub", "servicebus", "azureservicebus", "EventProcessorHost",
            "AMQP", "Amqp", "BYOM", "Byom",
            "copilot-SDK", "copilot-SDK-service", "@github/copilot-SDK",
            "azd", "azqr", "az login", "az quota", "az bicep",
            "dev/test", "nspell"
        };
        
        foreach (var term in terms)
        {
            // Only wrap if not already inside backticks
            var pattern = $@"(?<!`)(\b{Regex.Escape(term)}\b)(?!`)";
            content = Regex.Replace(content, pattern, $"`{term}`");
        }
        return content;
    }

    /// <summary>
    /// Temporarily extracts backtick spans, applies a transform, then restores them.
    /// This prevents transforms from modifying content inside backticks.
    /// </summary>
    private static string ApplyWithBacktickProtection(string text, Func<string, string> transform)
    {
        var placeholders = new Dictionary<string, string>();
        int counter = 0;

        // Replace backtick spans with placeholders
        var protectedText = Regex.Replace(text, @"`[^`]+`", match =>
        {
            var placeholder = $"\x00BTCK{counter++}\x00";
            placeholders[placeholder] = match.Value;
            return placeholder;
        });

        // Apply the transform
        protectedText = transform(protectedText);

        // Restore backtick spans
        foreach (var (placeholder, original) in placeholders)
        {
            protectedText = protectedText.Replace(placeholder, original);
        }

        return protectedText;
    }

    internal static (string frontmatter, string body) SplitFrontmatter(string content)
    {
        if (!content.TrimStart().StartsWith("---"))
            return ("", content);

        var match = Regex.Match(content, @"^(---\s*\n.*?\n---\s*\n?)", RegexOptions.Singleline);
        if (!match.Success)
            return ("", content);

        return (match.Groups[1].Value, content[match.Groups[1].Length..]);
    }

    internal static string AddIntroductoryCommas(string content)
    {
        var result = content;

        // Handle multi-word phrases first (longer match before shorter)
        foreach (var phrase in IntroductoryPhrases)
        {
            // Match phrase at start of line (or after sentence boundary) NOT already followed by comma
            var pattern = $@"(?<=^|\. ){Regex.Escape(phrase)} (?!,)";
            result = Regex.Replace(result, pattern, $"{phrase}, ", RegexOptions.Multiline);
        }

        // Handle single introductory words
        foreach (var word in IntroductoryWords)
        {
            var pattern = $@"(?<=^|\. ){Regex.Escape(word)} (?!,)";
            result = Regex.Replace(result, pattern, $"{word}, ", RegexOptions.Multiline);
        }

        return result;
    }

    internal static string WrapBareSkillNames(string content)
    {
        // Process line by line to skip headings and frontmatter lines
        var lines = content.Split('\n');
        var result = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            // Skip headings and frontmatter delimiters
            if (trimmed.StartsWith('#') || trimmed.StartsWith("---"))
            {
                result.Add(line);
                continue;
            }

            result.Add(WrapBareSkillNamesInLine(line));
        }

        return string.Join('\n', result);
    }

    private static string WrapBareSkillNamesInLine(string line)
    {
        // Use alternation to skip backtick spans, markdown link URLs, and raw URLs.
        // Group 1 = backtick span (preserve), Group 2 = link URL (preserve),
        // Group 3 = raw URL (preserve), Group 4 = bare skill name (wrap)
        return Regex.Replace(line,
            @"(`[^`]+`)" +            // backtick span — capture group 1
            @"|(\]\([^)]+\))" +        // markdown link URL — capture group 2
            @"|(https?://\S+)" +       // raw URL — capture group 3
            @"|\b(azure-[a-z][a-z0-9]*(?:-[a-z][a-z0-9]*)*)\b", // bare skill name — group 4
            match =>
            {
                if (match.Groups[1].Success) return match.Groups[1].Value;
                if (match.Groups[2].Success) return match.Groups[2].Value;
                if (match.Groups[3].Success) return match.Groups[3].Value;
                return $"`{match.Groups[4].Value}`";
            });
    }

    internal static string SplitLongSentences(string content)
    {
        var lines = content.Split('\n');
        var result = new List<string>();

        foreach (var line in lines)
        {
            // Skip frontmatter, headings, list items, table rows, and code blocks
            if (line.TrimStart().StartsWith("---") ||
                line.TrimStart().StartsWith('#') ||
                line.TrimStart().StartsWith('-') ||
                line.TrimStart().StartsWith('|') ||
                line.TrimStart().StartsWith("```") ||
                line.TrimStart().StartsWith('`'))
            {
                result.Add(line);
                continue;
            }

            result.Add(SplitLongSentencesInLine(line));
        }

        return string.Join('\n', result);
    }

    private static string SplitLongSentencesInLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return line;

        // Extract sentences (split on ". " but not inside backticks/links)
        var sentences = SplitIntoSentences(line);
        var rebuilt = new List<string>();

        foreach (var sentence in sentences)
        {
            var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= 30)
            {
                rebuilt.Add(sentence);
                continue;
            }

            // Find a conjunction after word 20 to split at
            var split = TrySplitAtConjunction(sentence, words);
            if (split != null)
            {
                rebuilt.Add(split);
            }
            else
            {
                rebuilt.Add(sentence);
            }
        }

        return string.Join(" ", rebuilt);
    }

    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var current = 0;

        for (int i = 0; i < text.Length - 1; i++)
        {
            if (text[i] == '.' && i + 1 < text.Length && text[i + 1] == ' ')
            {
                // Don't split inside backticks
                var beforeDot = text[current..(i + 1)];
                var backtickCount = beforeDot.Count(c => c == '`');
                if (backtickCount % 2 == 0)
                {
                    sentences.Add(text[current..(i + 1)].Trim());
                    current = i + 2;
                }
            }
        }

        if (current < text.Length)
        {
            var remaining = text[current..].Trim();
            if (!string.IsNullOrEmpty(remaining))
                sentences.Add(remaining);
        }

        return sentences;
    }

    private static string? TrySplitAtConjunction(string sentence, string[] words)
    {
        // Count words up to positions in the original string to find a conjunction after word 20
        foreach (var conjunction in SplitConjunctions)
        {
            var searchStart = 0;
            // Skip to approximately word 20 by counting spaces
            var spaceCount = 0;
            for (int i = 0; i < sentence.Length && spaceCount < 20; i++)
            {
                if (sentence[i] == ' ') spaceCount++;
                searchStart = i;
            }

            var conjIdx = sentence.IndexOf(conjunction, searchStart, StringComparison.OrdinalIgnoreCase);
            if (conjIdx > 0)
            {
                var before = sentence[..conjIdx].TrimEnd();
                var after = sentence[(conjIdx + conjunction.Length)..].TrimStart();

                if (string.IsNullOrEmpty(after)) continue;

                // Capitalize the first letter of the new sentence
                var afterCapitalized = char.ToUpper(after[0]) + after[1..];

                // Ensure the first part ends with a period
                if (!before.EndsWith('.'))
                    before += ".";

                return $"{before} {afterCapitalized}";
            }
        }

        return null;
    }

    private string ExpandAcronymsFirstUse(string content)
    {
        var allMatches = new List<(int Index, int Length, string Replacement)>();

        foreach (var (acronym, expansion) in _acronyms)
        {
            // Match the acronym only when NOT already expanded (not preceded by "expansion (")
            var pattern = $@"\b{Regex.Escape(acronym)}\b";
            var match = Regex.Match(content, pattern);
            if (match.Success)
            {
                // Skip if already expanded — check if preceded by "expansion ("
                var alreadyExpanded = false;
                var expectedPrefix = $"{expansion} (";
                if (match.Index >= expectedPrefix.Length)
                {
                    var preceding = content[(match.Index - expectedPrefix.Length)..match.Index];
                    if (preceding.Equals(expectedPrefix, StringComparison.OrdinalIgnoreCase))
                        alreadyExpanded = true;
                }

                // Also skip if inside parentheses following the expansion
                if (!alreadyExpanded && match.Index > 0)
                {
                    var beforeMatch = content[..match.Index];
                    var lastOpenParen = beforeMatch.LastIndexOf('(');
                    var lastCloseParen = beforeMatch.LastIndexOf(')');
                    if (lastOpenParen > lastCloseParen && lastOpenParen >= 0)
                    {
                        // Inside parentheses — check if the text before "(" is the expansion
                        var textBeforeParen = content[..lastOpenParen].TrimEnd();
                        if (textBeforeParen.EndsWith(expansion, StringComparison.OrdinalIgnoreCase))
                            alreadyExpanded = true;
                    }
                }

                if (!alreadyExpanded)
                {
                    var expanded = $"{expansion} ({acronym})";
                    allMatches.Add((match.Index, match.Length, expanded));
                }
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

    private List<TextReplacement> LoadReplacements(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("Text replacements data not provided (null or empty)");
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<TextReplacement>>(json, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse text replacements JSON, using empty list");
            return [];
        }
    }

    private Dictionary<string, string> LoadAcronyms(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("Acronyms data not provided (null or empty)");
            return new();
        }

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
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse acronyms JSON, using empty dictionary");
            return new();
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Rewrites "Run/Execute/Use X to Y" → "To Y, run/execute/use X" for goal-before-action style.
    /// Only applies to prose lines (not headings, list items, code blocks, or table rows).
    /// </summary>
    internal static string RewriteGoalBeforeAction(string content)
    {
        var lines = content.Split('\n');
        var result = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('#') || trimmed.StartsWith('-') ||
                trimmed.StartsWith('|') || trimmed.StartsWith("```") ||
                trimmed.StartsWith('`'))
            {
                result.Add(line);
                continue;
            }

            result.Add(GoalBeforeActionRegex().Replace(line, match =>
            {
                var verb = match.Groups[1].Value;
                var action = match.Groups[2].Value.TrimEnd();
                var goal = match.Groups[3].Value.TrimEnd();

                // Remove trailing period from goal for clean rewrite
                if (goal.EndsWith('.'))
                    goal = goal[..^1].TrimEnd();

                // Lowercase the original verb in the rewritten form
                var lowerVerb = char.ToLower(verb[0]) + verb[1..];

                return $"To {goal}, {lowerVerb} {action}.";
            }));
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// Removes colons after bold labels in prerequisite/list contexts.
    /// Transforms "**Label:** text" → "**Label** text".
    /// </summary>
    internal static string RemoveBoldLabelColons(string content)
    {
        return BoldLabelColonRegex().Replace(content, "**${label}** ");
    }

    /// <summary>
    /// Detects and removes consecutive sentences with the same normalized meaning.
    /// Two sentences are considered duplicates if they share 80%+ of their meaningful words.
    /// </summary>
    internal static string RemoveConsecutiveDuplicateSentences(string content)
    {
        var lines = content.Split('\n');
        var result = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('#') || trimmed.StartsWith('-') ||
                trimmed.StartsWith('|') || trimmed.StartsWith("```") ||
                string.IsNullOrWhiteSpace(trimmed))
            {
                result.Add(line);
                continue;
            }

            result.Add(RemoveDuplicatesInLine(line));
        }

        return string.Join('\n', result);
    }

    private static string RemoveDuplicatesInLine(string line)
    {
        var sentences = SplitIntoSentences(line);
        if (sentences.Count < 2)
            return line;

        var kept = new List<string> { sentences[0] };

        for (int i = 1; i < sentences.Count; i++)
        {
            if (!AreSentencesDuplicate(sentences[i - 1], sentences[i]))
            {
                kept.Add(sentences[i]);
            }
        }

        return string.Join(" ", kept);
    }

    internal static bool AreSentencesDuplicate(string a, string b)
    {
        var wordsA = ExtractMeaningfulWords(a);
        var wordsB = ExtractMeaningfulWords(b);

        if (wordsA.Count == 0 || wordsB.Count == 0)
            return false;

        var intersection = wordsA.Intersect(wordsB, StringComparer.OrdinalIgnoreCase).Count();
        var maxCount = Math.Max(wordsA.Count, wordsB.Count);

        return (double)intersection / maxCount >= 0.8;
    }

    private static List<string> ExtractMeaningfulWords(string sentence)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "is", "are", "was", "were", "be", "been",
            "to", "of", "in", "for", "on", "with", "at", "by", "from",
            "and", "or", "but", "not", "this", "that", "it", "its", "you", "your"
        };

        return Regex.Replace(sentence, @"[^\w\s]", "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !stopWords.Contains(w) && w.Length > 1)
            .ToList();
    }

    [GeneratedRegex(@"^(Run|Execute|Use)\s+(.+?)\s+to\s+(.+?)(?:\.|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex GoalBeforeActionRegex();

    [GeneratedRegex(@"\*\*(?<label>[^*:]+):\*\*\s?")]
    private static partial Regex BoldLabelColonRegex();

    private record TextReplacement(string Parameter, string NaturalLanguage);
    private record AcronymEntry(string Acronym, string Expansion, string? ContextPattern = null, string? ExpandedForm = null);
}
