using System.Text.RegularExpressions;
using PipelineRunner.Context;
using PipelineRunner.Contracts;

namespace PipelineRunner.Validation;

/// <summary>
/// Post-validator for Step 3 (ToolGeneration). Detects leaked template tokens
/// such as Handlebars placeholders ({{...}}) and unrendered template variables
/// in the final tool .md files.
/// </summary>
public sealed class ToolGenerationOutputValidator : IPostValidator
{
    private static readonly Regex HandlebarsTokenRegex = new(
        @"\{\{(EXAMPLE_PROMPTS_CONTENT|PARAMETERS_CONTENT|ANNOTATIONS_CONTENT|toolName|serviceBrandName|commandName)\}\}",
        RegexOptions.Compiled);

    private static readonly Regex TemplateVariableRegex = new(
        @"\{(REQUIRED_PARAM_COUNT|PARAM_NAMES|INCOMPLETE_|PLACEHOLDER_)\w*\}",
        RegexOptions.Compiled);

    private const int MinimumContentLength = 100;

    public string Name => "ToolGenerationOutputValidator";

    public async ValueTask<ValidatorResult> ValidateAsync(
        PipelineContext context, IPipelineStep step, CancellationToken cancellationToken)
    {
        var toolsDirectory = Path.Combine(context.OutputPath, "tools");
        var warnings = new List<string>();

        if (!Directory.Exists(toolsDirectory))
            return new ValidatorResult(Name, true, warnings);

        var toolFiles = Directory.GetFiles(toolsDirectory, "*.md");
        if (toolFiles.Length == 0)
            return new ValidatorResult(Name, true, warnings);

        var leakedFiles = new List<string>();

        foreach (var toolFile in toolFiles)
        {
            var fileName = Path.GetFileName(toolFile);
            var content = await File.ReadAllTextAsync(toolFile, cancellationToken);

            if (content.Length < MinimumContentLength)
            {
                warnings.Add($"{fileName}: suspiciously short ({content.Length} chars, expected {MinimumContentLength}+)");
                leakedFiles.Add(fileName);
                continue;
            }

            var handlebarsMatches = HandlebarsTokenRegex.Matches(content);
            if (handlebarsMatches.Count > 0)
            {
                var tokens = handlebarsMatches.Select(m => m.Value).Distinct();
                warnings.Add($"{fileName}: leaked Handlebars tokens: {string.Join(", ", tokens)}");
                leakedFiles.Add(fileName);
            }

            var templateVarMatches = TemplateVariableRegex.Matches(content);
            if (templateVarMatches.Count > 0)
            {
                var vars = templateVarMatches.Select(m => m.Value).Distinct();
                warnings.Add($"{fileName}: leaked template variables: {string.Join(", ", vars)}");
                leakedFiles.Add(fileName);
            }
        }

        var success = leakedFiles.Count == 0;
        if (!success)
        {
            warnings.Insert(0, $"Template token leakage detected in {leakedFiles.Count} of {toolFiles.Length} tool file(s)");
        }

        return new ValidatorResult(Name, success, warnings);
    }
}
