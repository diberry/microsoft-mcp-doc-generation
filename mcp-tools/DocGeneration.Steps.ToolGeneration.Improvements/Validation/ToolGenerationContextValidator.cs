// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shared.Validation;
using ToolGeneration_Improved.Models;

namespace ToolGeneration_Improved.Validation;

/// <summary>
/// Seam validator that checks the structural integrity of a
/// <see cref="ToolGenerationContext"/> before the LLM call is dispatched.
/// </summary>
public sealed class ToolGenerationContextValidator : IPreAiValidator<ToolGenerationContext>
{
    /// <inheritdoc />
    public Task<PreAiValidationResult> ValidateAsync(ToolGenerationContext context, CancellationToken cancellationToken)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(context.ToolName))
        {
            errors.Add(new ValidationError("ToolName", "ToolName must not be empty.", ValidationSeverity.Error));
        }

        if (string.IsNullOrWhiteSpace(context.ComposedContent))
        {
            errors.Add(new ValidationError("ComposedContent", "ComposedContent must not be empty.", ValidationSeverity.Error));
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
