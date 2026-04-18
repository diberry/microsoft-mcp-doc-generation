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

    // Known typo patterns in generated links
    private static readonly Regex[] BadLinkPatterns =
    [
        new(@"github-cilot", RegexOptions.IgnoreCase),
        new(@"github-copiliot", RegexOptions.IgnoreCase),
        new(@"micosoft|microsft|microsfot", RegexOptions.IgnoreCase),
        new(@"/docs/azure/", RegexOptions.IgnoreCase),
        new(@"learn\.microsoft\.com/en-us/en-us/", RegexOptions.IgnoreCase),
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

        // LINK_TYPO: Check for known bad link patterns
        var linkMatches = Regex.Matches(renderedContent, @"\[([^\]]*)\]\(([^)]+)\)");
        foreach (Match linkMatch in linkMatches)
        {
            var url = linkMatch.Groups[2].Value;
            foreach (var badPattern in BadLinkPatterns)
            {
                if (badPattern.IsMatch(url))
                {
                    warnings.Add($"LINK_TYPO: Suspicious URL pattern in '{url}' — possible typo");
                }
            }
        }

        // NEGATIVE_IN_POSITIVE: Detect redirect/negative items in "When to use" section
        var whenSectionMatch = Regex.Match(renderedContent,
            @"(?:^|\n)###?\s*When to use.*?\n(.*?)(?=\n##[^#]|\z)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (whenSectionMatch.Success)
        {
            var whenSection = whenSectionMatch.Groups[1].Value;
            var bulletItems = Regex.Matches(whenSection, @"^[ \t]*-\s+(.+)$", RegexOptions.Multiline);
            foreach (Match bullet in bulletItems)
            {
                var item = bullet.Groups[1].Value.Trim();
                // Detect redirect patterns: "(use X)", "(use X instead)", "use X for this"
                if (Regex.IsMatch(item, @"\(use\s+\S+", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(item, @"\binstead\b", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(item, @"^Not\s+for\b", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(item, @"^Do\s+not\s+use\b", RegexOptions.IgnoreCase))
                {
                    warnings.Add($"NEGATIVE_IN_POSITIVE: Item in 'When to use' appears negative/redirect: \"{item}\"");
                }
            }
        }

        // PROMPT_COUNT: Validate example prompts in rendered content
        var promptSectionMatch = Regex.Match(renderedContent,
            @"(?:^|\n)###?\s*Example prompts.*?\n(.*?)(?=\n##[^#]|\z)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (promptSectionMatch.Success)
        {
            var promptSection = promptSectionMatch.Groups[1].Value;
            var promptBullets = Regex.Matches(promptSection, @"^[ \t]*-\s+", RegexOptions.Multiline);
            if (promptBullets.Count == 0)
            {
                errors.Add("PROMPT_COUNT: No example prompts in rendered content");
            }
            else if (promptBullets.Count < 5)
            {
                warnings.Add($"PROMPT_COUNT: Only {promptBullets.Count} example prompts (recommended minimum: 5)");
            }
        }

        // PROMPT_SOURCE: Warn when trigger test file is missing (fallback prompts used)
        if (triggerData.ShouldTrigger.Count == 0 && skillData.UseFor.Count > 0)
        {
            warnings.Add("PROMPT_SOURCE: No triggers.test.ts found; example prompts generated from UseFor items");
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
