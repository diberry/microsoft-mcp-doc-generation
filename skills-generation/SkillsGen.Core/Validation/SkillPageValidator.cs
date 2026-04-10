using System.Text.RegularExpressions;
using SkillsGen.Core.Models;

namespace SkillsGen.Core.Validation;

public class SkillPageValidator : ISkillPageValidator
{
    private static readonly string[] RequiredSections =
    [
        "## Prerequisites",
        "## When to use",
        "## What it provides"
    ];

    public SkillValidationResult Validate(string renderedContent, int tier, SkillData skillData, TriggerData triggerData)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(renderedContent))
        {
            errors.Add("EMPTY: Rendered content is empty");
            return new SkillValidationResult(false, errors, warnings, 0, 0);
        }

        // COMPLETENESS: Required sections present
        foreach (var section in RequiredSections)
        {
            if (!renderedContent.Contains(section, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"COMPLETENESS: Missing required section '{section}'");
            }
        }

        // PREREQ_COPILOT: GitHub Copilot listed as required tool
        if (!renderedContent.Contains("GitHub Copilot", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("PREREQ_COPILOT: GitHub Copilot not listed as required tool");
        }

        // GROUNDING: Example prompts sourced from trigger data
        if (triggerData.ShouldTrigger.Count > 0)
        {
            var hasAnyPrompt = triggerData.ShouldTrigger.Any(t =>
                renderedContent.Contains(t, StringComparison.OrdinalIgnoreCase));
            if (!hasAnyPrompt)
            {
                warnings.Add("GROUNDING: No example prompts sourced from trigger data");
            }
        }

        // WORD_COUNT
        var wordCount = CountWords(renderedContent);
        var (minWords, maxWords) = tier == 1 ? (100, 500) : (50, 200);
        if (wordCount < minWords)
        {
            warnings.Add($"WORD_COUNT: Content has {wordCount} words, minimum for Tier {tier} is {minWords}");
        }
        if (wordCount > maxWords)
        {
            warnings.Add($"WORD_COUNT: Content has {wordCount} words, maximum for Tier {tier} is {maxWords}");
        }

        // FRONTMATTER: Required fields present
        if (!renderedContent.TrimStart().StartsWith("---"))
        {
            errors.Add("FRONTMATTER: Missing YAML frontmatter");
        }
        else
        {
            var frontmatterMatch = Regex.Match(renderedContent, @"^---\s*\n(.*?)\n---", RegexOptions.Singleline);
            if (frontmatterMatch.Success)
            {
                var fm = frontmatterMatch.Groups[1].Value;
                if (!fm.Contains("title:", StringComparison.OrdinalIgnoreCase))
                    errors.Add("FRONTMATTER: Missing 'title' field");
                if (!fm.Contains("description:", StringComparison.OrdinalIgnoreCase))
                    errors.Add("FRONTMATTER: Missing 'description' field");
            }
        }

        // ACROLINX_URLS: No absolute learn.microsoft.com URLs
        if (Regex.IsMatch(renderedContent, @"https?://learn\.microsoft\.com"))
        {
            warnings.Add("ACROLINX_URLS: Content contains absolute learn.microsoft.com URLs");
        }

        // PREREQ_DUPLICATE: Check for duplicate "GitHub Copilot" in prerequisites
        var copilotMatches = Regex.Matches(renderedContent,
            @"(?:^|\n)\s*-\s+\*{0,2}GitHub Copilot\*{0,2}", RegexOptions.IgnoreCase);
        if (copilotMatches.Count > 1)
        {
            warnings.Add("PREREQ_DUPLICATE: 'GitHub Copilot' appears multiple times in prerequisites");
        }

        // FRAGMENT: Check for "Work with X" fragment pattern in bullet points
        var fragmentMatches = Regex.Matches(renderedContent,
            @"^[ \t]*-\s+Work with\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (fragmentMatches.Count > 0)
        {
            warnings.Add($"FRAGMENT: {fragmentMatches.Count} bullet(s) use vague 'Work with' fragment pattern");
        }

        var sectionCount = Regex.Matches(renderedContent, @"^## ", RegexOptions.Multiline).Count;

        return new SkillValidationResult(errors.Count == 0, errors, warnings, wordCount, sectionCount);
    }

    private static int CountWords(string text)
    {
        // Strip frontmatter
        var body = Regex.Replace(text, @"^---.*?---\s*", "", RegexOptions.Singleline);
        // Strip HTML comments
        body = Regex.Replace(body, @"<!--.*?-->", "", RegexOptions.Singleline);
        // Strip markdown formatting
        body = Regex.Replace(body, @"[#*|`\[\]\(\)\-]", " ");
        // Count words
        return body.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
