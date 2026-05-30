// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Shared.Validation;
using ToolGeneration_Improved.Models;

namespace ToolGeneration_Improved.Validation;

/// <summary>
/// Seam validator that estimates the input-token footprint of a
/// <see cref="ToolGenerationContext"/> and rejects it when the estimated
/// token count exceeds the configured budget.
/// </summary>
public sealed class ToolGenerationBudgetValidator : IPreAiValidator<ToolGenerationContext>
{
    /// <summary>Characters-per-token estimate used for all token calculations.</summary>
    internal const int CharsPerToken = 4;

    /// <summary>Maximum input tokens allowed for the tool-generation AI stage.</summary>
    internal const int InputTokenBudget = 100_000;

    /// <inheritdoc />
    public Task<PreAiValidationResult> ValidateAsync(ToolGenerationContext context, CancellationToken cancellationToken)
    {
        var estimatedTokens = context.ComposedContent.Length / CharsPerToken;

        if (estimatedTokens > InputTokenBudget)
        {
            return Task.FromResult(new PreAiValidationResult(
                false,
                [
                    new ValidationError(
                        "ComposedContent",
                        $"Estimated {estimatedTokens:N0} input tokens exceeds budget of {InputTokenBudget:N0}.",
                        ValidationSeverity.Error)
                ],
                estimatedTokens,
                InputTokenBudget));
        }

        return Task.FromResult(PreAiValidationResult.Pass(estimatedTokens, InputTokenBudget));
    }
}
