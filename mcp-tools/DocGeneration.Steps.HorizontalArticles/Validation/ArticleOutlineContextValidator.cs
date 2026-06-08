// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HorizontalArticleGenerator.Models;
using Shared.Validation;

namespace HorizontalArticleGenerator.Validation;

/// <summary>
/// Seam validator that checks the structural integrity of an
/// <see cref="ArticleOutlineContext"/> before the LLM call is dispatched.
/// </summary>
public sealed class ArticleOutlineContextValidator : IPreAiValidator<ArticleOutlineContext>
{
    /// <inheritdoc />
    public Task<PreAiValidationResult> ValidateAsync(ArticleOutlineContext context, CancellationToken cancellationToken)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(context.ArticleTitle))
        {
            errors.Add(new ValidationError("ArticleTitle", "ArticleTitle must not be empty.", ValidationSeverity.Error));
        }

        if (context.Sections.Count < 2)
        {
            errors.Add(new ValidationError(
                "Sections",
                $"ArticleOutlineContext must contain at least 2 sections, but found {context.Sections.Count}.",
                ValidationSeverity.Error));
        }
        else
        {
            for (var i = 0; i < context.Sections.Count; i++)
            {
                if (context.Sections[i].EvidenceItems.Count == 0)
                {
                    errors.Add(new ValidationError(
                        $"Sections[{i}].EvidenceItems",
                        $"Section '{context.Sections[i].Heading}' at index {i} has no evidence items.",
                        ValidationSeverity.Error));
                }
            }
        }

        if (context.SchemaVersion != "1.0")
        {
            errors.Add(new ValidationError(
                "SchemaVersion",
                $"Unrecognized SchemaVersion '{context.SchemaVersion}'. Expected '1.0'.",
                ValidationSeverity.Error));
        }

        return Task.FromResult(errors.Count == 0
            ? PreAiValidationResult.Pass()
            : PreAiValidationResult.Fail([.. errors]));
    }
}
