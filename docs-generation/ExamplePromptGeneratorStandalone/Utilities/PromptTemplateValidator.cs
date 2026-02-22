using System.Text.RegularExpressions;

namespace ExamplePromptGeneratorStandalone.Utilities;

/// <summary>
/// Validates that a user prompt template has been fully resolved —
/// no unreplaced placeholders or Handlebars blocks remain.
/// </summary>
public static partial class PromptTemplateValidator
{
    /// <summary>
    /// All simple {PLACEHOLDER} tokens used in the user prompt template.
    /// </summary>
    private static readonly string[] SimplePlaceholders =
    [
        "{TOOL_NAME}",
        "{TOOL_COMMAND}",
        "{TOOL_DESCRIPTION}",
        "{ACTION_VERB}",
        "{RESOURCE_TYPE}",
        "{PROMPT_COUNT}"
    ];

    /// <summary>
    /// Validates that the prompt has no unreplaced template tokens.
    /// Returns a list of problems found (empty = valid).
    /// </summary>
    public static List<string> Validate(string prompt)
    {
        var problems = new List<string>();

        if (string.IsNullOrEmpty(prompt))
        {
            problems.Add("Prompt is null or empty");
            return problems;
        }

        // Check for unreplaced simple placeholders
        foreach (var placeholder in SimplePlaceholders)
        {
            if (prompt.Contains(placeholder))
            {
                problems.Add($"Unreplaced placeholder: {placeholder}");
            }
        }

        // Check for unreplaced Handlebars blocks ({{#each ...}}, {{/each}}, {{#if ...}}, {{/if}})
        if (HandlebarsBlockRegex().IsMatch(prompt))
        {
            var matches = HandlebarsBlockRegex().Matches(prompt);
            foreach (Match match in matches)
            {
                problems.Add($"Unreplaced Handlebars block: {match.Value}");
            }
        }

        // Check for unreplaced Handlebars expressions like {{name}}, {{description}}
        // but exclude angle-bracket placeholders like <subscription> which are intentional
        if (HandlebarsExpressionRegex().IsMatch(prompt))
        {
            var matches = HandlebarsExpressionRegex().Matches(prompt);
            foreach (Match match in matches)
            {
                problems.Add($"Unreplaced Handlebars expression: {match.Value}");
            }
        }

        return problems;
    }

    /// <summary>
    /// Validates the prompt and logs any problems. Returns true if valid.
    /// </summary>
    public static bool ValidateAndLog(string prompt, string toolCommand)
    {
        var problems = Validate(prompt);
        if (problems.Count == 0)
            return true;

        Console.WriteLine($"  ⚠️  Prompt template validation failed for '{toolCommand}':");
        foreach (var problem in problems)
        {
            Console.WriteLine($"      - {problem}");
        }
        return false;
    }

    // Matches {{#each ...}}, {{/each}}, {{#if ...}}, {{/if}}, {{else}}
    [GeneratedRegex(@"\{\{[#/](?:each|if|else)\b.*?\}\}", RegexOptions.Singleline)]
    private static partial Regex HandlebarsBlockRegex();

    // Matches {{word}} but NOT {{"text"}} or {{ with spaces that look like JSON
    [GeneratedRegex(@"\{\{(?!#|/)([a-zA-Z_]\w*)\}\}")]
    private static partial Regex HandlebarsExpressionRegex();
}
