// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HorizontalArticleGenerator.Models;
using Shared.Validation;

namespace HorizontalArticleGenerator.Validation;

/// <summary>
/// Seam validator that estimates the input-token footprint of an
/// <see cref="ArticleOutlineContext"/> and rejects it when the estimated
/// token count exceeds the configured budget.
/// </summary>
public sealed class ArticleOutlineBudgetValidator : IPreAiValidator<ArticleOutlineContext>
{
    /// <summary>Characters-per-token estimate used for all token calculations.</summary>
    internal const int CharsPerToken = 4;

    /// <summary>Maximum input tokens allowed for the horizontal-articles AI stage.</summary>
    public const int InputTokenBudget = 150_000;

    /// <inheritdoc />
    public Task<PreAiValidationResult> ValidateAsync(ArticleOutlineContext context, CancellationToken cancellationToken)
    {
        var totalChars = context.Sections
            .SelectMany(static section => section.EvidenceItems)
            .Sum(static item => item.Length);

        var estimatedTokens = totalChars / CharsPerToken;

        if (estimatedTokens > InputTokenBudget)
        {
            return Task.FromResult(new PreAiValidationResult(
                false,
                [
                    new ValidationError(
                        "Sections",
                        $"Estimated {estimatedTokens:N0} input tokens exceeds budget of {InputTokenBudget:N0}.",
                        ValidationSeverity.Error)
                ],
                estimatedTokens,
                InputTokenBudget));
        }

        return Task.FromResult(PreAiValidationResult.Pass(estimatedTokens, InputTokenBudget));
    }
}
