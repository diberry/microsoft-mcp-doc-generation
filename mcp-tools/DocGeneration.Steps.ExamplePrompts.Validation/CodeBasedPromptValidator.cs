using Shared;

namespace DocGeneration.Steps.ExamplePrompts.Validation;

public sealed class CodeBasedPromptValidator
{
    public CodeBasedPromptValidationResult ValidatePrompts(
        IReadOnlyList<string> prompts,
        CanonicalParameterManifest manifest)
    {
        var requiredParams = manifest.Parameters.Where(p => p.Required).ToList();
        if (requiredParams.Count == 0)
        {
            return new CodeBasedPromptValidationResult(
                IsValid: true,
                TotalPrompts: prompts.Count,
                TotalRequiredParameters: 0,
                Details: Array.Empty<ParameterValidationDetail>());
        }

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
            TotalRequiredParameters: requiredParams.Count,
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
