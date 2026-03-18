using Shared;

namespace DocGeneration.Steps.ExamplePrompts.Validation;

public sealed class CodeBasedPromptValidator
{
    public CodeBasedPromptValidationResult ValidatePrompts(
        IReadOnlyList<string> prompts,
        IReadOnlyList<string> requiredParameterNames)
    {
        if (requiredParameterNames.Count == 0)
        {
            return new CodeBasedPromptValidationResult(
                IsValid: true,
                TotalPrompts: prompts.Count,
                TotalRequiredParameters: 0,
                Details: Array.Empty<ParameterValidationDetail>());
        }

        var details = new List<ParameterValidationDetail>();
        var allCovered = true;

        foreach (var parameterName in requiredParameterNames)
        {
            var coverage = ParameterCoverageChecker.GetConcretePromptCoverage(
                prompts, parameterName, requiredParameterNames.Count);

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
