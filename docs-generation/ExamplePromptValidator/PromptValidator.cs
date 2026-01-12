// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ExamplePromptValidator;

/// <summary>
/// Validates that generated example prompts contain the required tool parameters.
/// Excludes common parameters like subscription, tenant, auth, and retry parameters.
/// </summary>
public class PromptValidator
{
    // Common parameters to exclude from validation (case-insensitive)
    private static readonly HashSet<string> _excludedParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "subscription-id",
        "subscription",
        "tenant-id",
        "tenant",
        "auth-method",
        "auth",
        "authentication",
        "retry-max-attempts",
        "retry-delay",
        "retry",
        "output",
        "output-format",
        "verbose",
        "debug",
        "help"
    };

    /// <summary>
    /// Validates a single example prompt against a list of required parameters.
    /// </summary>
    /// <param name="prompt">The example prompt text to validate</param>
    /// <param name="requiredParameters">List of required parameter names (will be filtered for exclusions)</param>
    /// <returns>Validation result containing success status and missing parameters</returns>
    public static ValidationResult ValidatePrompt(string prompt, List<string> requiredParameters)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "Prompt is empty or null"
            };
        }

        // Filter out excluded parameters
        var parametersToCheck = requiredParameters
            .Where(p => !_excludedParameters.Contains(p))
            .ToList();

        if (parametersToCheck.Count == 0)
        {
            // No non-excluded parameters to check - consider valid
            return new ValidationResult { IsValid = true };
        }

        var missingParameters = new List<string>();
        var promptLower = prompt.ToLowerInvariant();

        foreach (var param in parametersToCheck)
        {
            // Check if parameter name appears in the prompt (with various formats)
            // Look for: parameter-name, parameter_name, "parameter name", or just parameter name
            var paramLower = param.ToLowerInvariant();
            var paramWithSpaces = paramLower.Replace("-", " ").Replace("_", " ");
            
            // Check multiple formats
            bool found = promptLower.Contains(paramLower) ||
                        promptLower.Contains(paramWithSpaces) ||
                        promptLower.Contains(param.Replace("-", "_").ToLowerInvariant());

            if (!found)
            {
                missingParameters.Add(param);
            }
        }

        return new ValidationResult
        {
            IsValid = missingParameters.Count == 0,
            MissingParameters = missingParameters,
            ErrorMessage = missingParameters.Count > 0 
                ? $"Missing required parameters: {string.Join(", ", missingParameters)}"
                : null
        };
    }

    /// <summary>
    /// Validates multiple example prompts against required parameters.
    /// </summary>
    /// <param name="prompts">List of example prompt texts</param>
    /// <param name="requiredParameters">List of required parameter names</param>
    /// <returns>Aggregated validation result for all prompts</returns>
    public static AggregatedValidationResult ValidatePrompts(List<string> prompts, List<string> requiredParameters)
    {
        if (prompts == null || prompts.Count == 0)
        {
            return new AggregatedValidationResult
            {
                IsValid = false,
                ErrorMessage = "No prompts provided for validation"
            };
        }

        var results = prompts.Select(p => ValidatePrompt(p, requiredParameters)).ToList();
        var allValid = results.All(r => r.IsValid);

        // Collect all unique missing parameters across all prompts
        var allMissingParameters = results
            .SelectMany(r => r.MissingParameters)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        return new AggregatedValidationResult
        {
            IsValid = allValid,
            TotalPrompts = prompts.Count,
            ValidPrompts = results.Count(r => r.IsValid),
            InvalidPrompts = results.Count(r => !r.IsValid),
            AllMissingParameters = allMissingParameters,
            IndividualResults = results,
            ErrorMessage = allValid ? null : $"{results.Count(r => !r.IsValid)} of {prompts.Count} prompts are missing required parameters"
        };
    }
}

/// <summary>
/// Result of validating a single prompt.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> MissingParameters { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Aggregated result of validating multiple prompts.
/// </summary>
public class AggregatedValidationResult
{
    public bool IsValid { get; set; }
    public int TotalPrompts { get; set; }
    public int ValidPrompts { get; set; }
    public int InvalidPrompts { get; set; }
    public List<string> AllMissingParameters { get; set; } = new();
    public List<ValidationResult> IndividualResults { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
