using Shared;

namespace DocGeneration.Steps.ExamplePrompts.Validation;

public sealed class CodeBasedPromptValidator
{
    public CodeBasedPromptValidationResult ValidatePrompts(
        IReadOnlyList<string> prompts,
        IReadOnlyList<string> requiredParameterNames,
        IReadOnlyDictionary<string, string>? descriptionsByParameter = null,
        CanonicalParameterManifest? manifest = null)
    {
        if (requiredParameterNames.Count == 0)
        {
            return new CodeBasedPromptValidationResult(
                IsValid: true,
                TotalPrompts: prompts.Count,
                TotalRequiredParameters: 0,
                Details: Array.Empty<ParameterValidationDetail>());
        }

        // When a canonical manifest is supplied, use the evaluator (Ambiguous is never covered).
        if (manifest != null)
        {
            return ValidateWithCanonicalManifest(prompts, manifest);
        }

        // Legacy path: no manifest — use old heuristic.
        var details = new List<ParameterValidationDetail>();
        var allCovered = true;

        foreach (var parameterName in requiredParameterNames)
        {
            string? description = null;
            descriptionsByParameter?.TryGetValue(parameterName, out description);

            var coverage = ParameterCoverageChecker.GetConcretePromptCoverage(
                prompts, parameterName, requiredParameterNames.Count, description);

            var effectivelyCovered = coverage.Covered || coverage.PlaceholderDetected;
            if (!effectivelyCovered)
            {
                allCovered = false;
            }

            details.Add(new ParameterValidationDetail(
                ParameterName: parameterName,
                Covered: coverage.Covered,
                PlaceholderDetected: coverage.PlaceholderDetected));
        }

        return new CodeBasedPromptValidationResult(
            IsValid: allCovered,
            TotalPrompts: prompts.Count,
            TotalRequiredParameters: requiredParameterNames.Count,
            Details: details);
    }

    private static CodeBasedPromptValidationResult ValidateWithCanonicalManifest(
        IReadOnlyList<string> prompts,
        CanonicalParameterManifest manifest)
    {
        var coverageResult = CanonicalCoverageEvaluator.EvaluateParameterCoverage(prompts, manifest);

        var details = coverageResult.ParameterResults
            .Select(c => new ParameterValidationDetail(
                ParameterName: c.CanonicalName,
                Covered: c.Verdict == CoverageVerdict.Concrete,
                PlaceholderDetected: c.Verdict == CoverageVerdict.AuthorizedPlaceholder))
            .ToList();

        return new CodeBasedPromptValidationResult(
            IsValid: coverageResult.AllRequiredCovered,
            TotalPrompts: prompts.Count,
            TotalRequiredParameters: manifest.Parameters.Count(p => p.Required),
            Details: details);
    }
}

public sealed record CodeBasedPromptValidationResult(
    bool IsValid,
    int TotalPrompts,
    int TotalRequiredParameters,
    IReadOnlyList<ParameterValidationDetail> Details);

public sealed record ParameterValidationDetail(
    string ParameterName,
    bool Covered,
    bool PlaceholderDetected);
