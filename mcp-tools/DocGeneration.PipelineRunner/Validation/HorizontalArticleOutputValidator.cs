using System.Text.RegularExpressions;
using PipelineRunner.Context;
using PipelineRunner.Contracts;

namespace PipelineRunner.Validation;

/// <summary>
/// Post-validator for Step 6 (HorizontalArticles). Detects incomplete or
/// malformed horizontal articles produced by AI generation — missing sections,
/// truncated content, or error artifacts.
/// </summary>
public sealed class HorizontalArticleOutputValidator : IPostValidator
{
    private static readonly Regex FrontmatterRegex = new(
        @"^---\s*\n(.*?)\n---",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private const int MinimumArticleLength = 1500;

    private static readonly string[] RequiredFrontmatterFields =
        ["title:", "ms.topic:", "ms.date:"];

    private static readonly string[] RequiredSections =
        ["## Prerequisites", "## Best practices", "## Related content"];

    public string Name => "HorizontalArticleOutputValidator";

    public async ValueTask<ValidatorResult> ValidateAsync(
        PipelineContext context, IPipelineStep step, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        if (!context.Items.TryGetValue("Namespace", out var nsValue) || nsValue is not string currentNamespace)
            return new ValidatorResult(Name, true, warnings);

        var articlesDir = Path.Combine(context.OutputPath, "horizontal-articles");
        var articlePath = Path.Combine(articlesDir, $"horizontal-article-{currentNamespace}.md");
        var errorPath = Path.Combine(articlesDir, $"error-{currentNamespace}.txt");
        var aiErrorPath = Path.Combine(articlesDir, $"error-{currentNamespace}-airesponse.txt");

        // Check for error artifacts
        if (File.Exists(errorPath) || File.Exists(aiErrorPath))
        {
            warnings.Add("Horizontal article generation produced error artifacts");
            if (File.Exists(errorPath))
                warnings.Add($"Error log: {Path.GetFileName(errorPath)}");
            if (File.Exists(aiErrorPath))
                warnings.Add($"AI response error: {Path.GetFileName(aiErrorPath)}");
            return new ValidatorResult(Name, false, warnings);
        }

        // Check article exists
        if (!File.Exists(articlePath))
        {
            warnings.Add($"Expected horizontal article not found: horizontal-article-{currentNamespace}.md");
            return new ValidatorResult(Name, false, warnings);
        }

        var content = await File.ReadAllTextAsync(articlePath, cancellationToken);

        // Check content length (truncation detection)
        if (content.Length < MinimumArticleLength)
        {
            warnings.Add($"Article appears truncated ({content.Length} chars, expected {MinimumArticleLength}+)");
        }

        // Validate frontmatter
        var fmMatch = FrontmatterRegex.Match(content);
        if (!fmMatch.Success)
        {
            warnings.Add("Missing or invalid frontmatter (---...---)");
        }
        else
        {
            var frontmatter = fmMatch.Groups[1].Value;
            foreach (var field in RequiredFrontmatterFields)
            {
                if (!frontmatter.Contains(field))
                    warnings.Add($"Frontmatter missing {field.TrimEnd(':')} field");
            }
        }

        // Validate required sections
        foreach (var section in RequiredSections)
        {
            if (!content.Contains(section, StringComparison.OrdinalIgnoreCase))
                warnings.Add($"Missing required section: {section.Replace("## ", "")}");
        }

        var success = !warnings.Any(w =>
            w.Contains("truncated") ||
            w.Contains("Missing or invalid frontmatter") ||
            w.Contains("error artifacts") ||
            w.Contains("Missing required section") ||
            w.Contains("Frontmatter missing"));

        return new ValidatorResult(Name, success, warnings);
    }
}
