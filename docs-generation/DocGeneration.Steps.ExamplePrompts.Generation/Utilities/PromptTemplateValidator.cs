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
        // but exclude patterns that appear in parameter descriptions as examples
        if (HandlebarsExpressionRegex().IsMatch(prompt))
        {
            var matches = HandlebarsExpressionRegex().Matches(prompt);
            foreach (Match match in matches)
            {
                // Skip matches that are clearly documentation examples, not unreplaced template variables
                // Check if the match is preceded by context words indicating it's an example
                int matchStart = match.Index;
                int contextStart = Math.Max(0, matchStart - 50); // Look back 50 chars for context
                string precedingContext = prompt.Substring(contextStart, matchStart - contextStart).ToLowerInvariant();
                
                // These words indicate the {{...}} is a documentation example, not a template variable
                string[] exampleIndicators = 
                [
                    "like ",
                    "such as ",
                    "example ",
                    "examples ",
                    "placeholder",
                    "e.g.",
                    "for example",
                    "including "
                ];
                
                bool isDocumentationExample = exampleIndicators.Any(indicator => 
                    precedingContext.Contains(indicator));
                
                if (!isDocumentationExample)
                {
                    problems.Add($"Unreplaced Handlebars expression: {match.Value}");
                }
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
