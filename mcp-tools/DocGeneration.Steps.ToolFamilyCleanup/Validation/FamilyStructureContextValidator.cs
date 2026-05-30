// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shared.Validation;
using ToolFamilyCleanup.Models;

namespace ToolFamilyCleanup.Validation;

/// <summary>
/// Seam validator that checks the structural integrity of a
/// <see cref="FamilyStructureContext"/> before the LLM call is dispatched.
/// </summary>
public sealed class FamilyStructureContextValidator : IPreAiValidator<FamilyStructureContext>
{
    /// <inheritdoc />
    public Task<PreAiValidationResult> ValidateAsync(FamilyStructureContext context, CancellationToken cancellationToken)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(context.FamilyName))
        {
            errors.Add(new ValidationError("FamilyName", "FamilyName must not be empty.", ValidationSeverity.Error));
        }

        if (context.Sections.Count == 0)
        {
            errors.Add(new ValidationError("Sections", "FamilyStructureContext must contain at least one section.", ValidationSeverity.Error));
        }
        else
        {
            for (var i = 0; i < context.Sections.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(context.Sections[i].Heading))
                {
                    errors.Add(new ValidationError(
                        $"Sections[{i}].Heading",
                        $"Section at index {i} has an empty Heading.",
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
