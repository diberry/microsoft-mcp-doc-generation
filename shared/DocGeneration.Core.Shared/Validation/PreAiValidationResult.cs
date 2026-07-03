// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Shared.Validation;

public sealed record PreAiValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationError> Errors,
    int? EstimatedPromptTokens = null,
    int? TokenBudget = null)
{
    public bool WithinBudget => TokenBudget is null || EstimatedPromptTokens <= TokenBudget;

    public static PreAiValidationResult Pass(int? estimatedTokens = null, int? budget = null)
        => new(true, Array.Empty<ValidationError>(), estimatedTokens, budget);

    public static PreAiValidationResult Fail(params ValidationError[] errors)
        => new(false, errors);
}

public sealed record ValidationError(string Field, string Message, ValidationSeverity Severity);

public enum ValidationSeverity
{
    Error,
    Warning
}
