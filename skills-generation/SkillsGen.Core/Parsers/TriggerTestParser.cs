using System.Text.RegularExpressions;
using SkillsGen.Core.Models;

namespace SkillsGen.Core.Parsers;

public partial class TriggerTestParser : ITriggerParser
{
    public TriggerData Parse(string? triggersContent)
    {
        if (string.IsNullOrWhiteSpace(triggersContent))
        {
            return new TriggerData([], [], null);
        }

        var shouldTrigger = ExtractStringArray(triggersContent, "shouldTriggerPrompts");
        var shouldNotTrigger = ExtractStringArray(triggersContent, "shouldNotTriggerPrompts");

        return new TriggerData(shouldTrigger, shouldNotTrigger, null);
    }

    private static List<string> ExtractStringArray(string content, string arrayName)
    {
        // Match patterns like: const shouldTriggerPrompts = [ ... ] or
        // const shouldTriggerPrompts: string[] = [ ... ]
        var pattern = $@"(?:const|let|var)\s+{Regex.Escape(arrayName)}\s*(?::\s*\w+\[\])?\s*=\s*\[(.*?)\]";
        var match = Regex.Match(content, pattern, RegexOptions.Singleline);
        if (!match.Success) return [];

        var arrayBody = match.Groups[1].Value;
        return ExtractQuotedStrings(arrayBody);
    }

    private static List<string> ExtractQuotedStrings(string text)
    {
        var items = new List<string>();

        // Match single-quoted, double-quoted, or backtick-quoted strings
        var matches = QuotedStringRegex().Matches(text);
        foreach (Match m in matches)
        {
            var value = m.Groups[1].Success ? m.Groups[1].Value :
                        m.Groups[2].Success ? m.Groups[2].Value :
                        m.Groups[3].Value;

            if (!string.IsNullOrWhiteSpace(value))
                items.Add(value);
        }

        return items;
    }

    [GeneratedRegex(@"'([^']*)'|""([^""]*)""|`([^`]*)`")]
    private static partial Regex QuotedStringRegex();
}
