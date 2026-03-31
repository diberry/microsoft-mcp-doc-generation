using System.Text.RegularExpressions;
using PipelineRunner.Context;
using PipelineRunner.Contracts;

namespace PipelineRunner.Validation;

/// <summary>
/// Post-validator for Step 3 (ToolGeneration). Detects leaked pipeline template
/// tokens (<<<TPL_LABEL_N>>>, <<<FROZEN_SECTION_N>>>, legacy __TPL_LABEL_N__
/// and **TPL_LABEL_N** formats), empty/truncated output files, and significant
/// content loss between raw and improved tool files.
/// </summary>
public sealed class ToolGenerationValidator : IPostValidator
{
    // Matches all known pipeline template token formats:
    //   <<<TPL_LABEL_0>>>, <<<FROZEN_SECTION_3>>>,
    //   __TPL_LABEL_2__, **TPL_LABEL_1**
    private static readonly Regex PipelineTokenRegex = new(
        @"(<<<TPL_LABEL_\d+>>>|<<<FROZEN_SECTION_\d+>>>|__TPL_LABEL_\d+__|\*\*TPL_LABEL_\d+\*\*)",
        RegexOptions.Compiled);

    private const int MinimumContentLength = 50;
    private const double ContentLossThreshold = 0.50;

    public string Name => "ToolGenerationValidator";

    public async ValueTask<ValidatorResult> ValidateAsync(
        PipelineContext context, IPipelineStep step, CancellationToken cancellationToken)
    {
        var toolsDirectory = Path.Combine(context.OutputPath, "tools");
        var rawDirectory = Path.Combine(context.OutputPath, "tools-raw");
        var warnings = new List<string>();

        if (!Directory.Exists(toolsDirectory))
            return new ValidatorResult(Name, true, warnings);

        var toolFiles = Directory.GetFiles(toolsDirectory, "*.md");
        if (toolFiles.Length == 0)
            return new ValidatorResult(Name, true, warnings);

        var flaggedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var toolFile in toolFiles)
        {
            var fileName = Path.GetFileName(toolFile);
            var content = await File.ReadAllTextAsync(toolFile, cancellationToken);

            // Check 1: Empty or truncated files
            if (content.Length < MinimumContentLength)
            {
                warnings.Add($"{fileName}: empty or truncated ({content.Length} chars, expected {MinimumContentLength}+)");
                flaggedFiles.Add(fileName);
                continue; // No point checking tokens in nearly-empty files
            }

            // Check 2: Pipeline template token leakage
            var tokenMatches = PipelineTokenRegex.Matches(content);
            if (tokenMatches.Count > 0)
            {
                var tokens = tokenMatches.Select(m => m.Value).Distinct();
                warnings.Add($"{fileName}: leaked pipeline template tokens: {string.Join(", ", tokens)}");
                flaggedFiles.Add(fileName);
            }

            // Check 3: Content loss detection (compare improved vs raw)
            if (Directory.Exists(rawDirectory))
            {
                var rawFile = Path.Combine(rawDirectory, fileName);
                if (File.Exists(rawFile))
                {
                    var rawContent = await File.ReadAllTextAsync(rawFile, cancellationToken);
                    if (rawContent.Length > 0)
                    {
                        var ratio = (double)content.Length / rawContent.Length;
                        if (ratio < (1.0 - ContentLossThreshold))
                        {
                            warnings.Add(
                                $"{fileName}: possible content loss — improved file is {100 - (int)(ratio * 100)}% shorter than raw input ({content.Length} vs {rawContent.Length} chars)");
                            flaggedFiles.Add(fileName);
                        }
                    }
                }
            }
        }

        var success = flaggedFiles.Count == 0;
        if (!success)
        {
            warnings.Insert(0, $"Tool generation issues detected in {flaggedFiles.Count} of {toolFiles.Length} tool file(s)");
        }

        return new ValidatorResult(Name, success, warnings);
    }
}
