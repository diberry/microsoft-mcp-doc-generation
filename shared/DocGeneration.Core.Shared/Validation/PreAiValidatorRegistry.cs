// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Validation;

/// <summary>
/// Registry that maps step names to their pre-AI validators.
/// </summary>
public sealed class PreAiValidatorRegistry
{
    private readonly Dictionary<string, List<IPreAiValidator>> _validators = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Type> _stageContextTypes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Declares the expected context type for a stage.
    /// </summary>
    public void DeclareStage<TContext>(string stageName)
    {
        _stageContextTypes[stageName] = typeof(TContext);
    }

    /// <summary>
    /// Registers a validator for a stage.
    /// </summary>
    public void Register<TContext>(string stageName, IPreAiValidator<TContext> validator)
    {
        if (_stageContextTypes.TryGetValue(stageName, out var declaredType) && declaredType != typeof(TContext))
        {
            throw new ValidatorContextMismatchException(stageName, declaredType, typeof(TContext));
        }

        if (!_validators.ContainsKey(stageName))
        {
            _validators[stageName] = new List<IPreAiValidator>();
        }

        _validators[stageName].Add(validator);
    }

    /// <summary>
    /// Runs all validators for a stage and returns an aggregated result.
    /// </summary>
    public async Task<PreAiValidationResult> ValidateAsync(string stageName, object context, CancellationToken cancellationToken)
    {
        if (!_validators.TryGetValue(stageName, out var validators) || validators.Count == 0)
        {
            return PreAiValidationResult.Pass();
        }

        var allErrors = new List<ValidationError>();
        int? estimatedTokens = null;
        int? tokenBudget = null;

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            allErrors.AddRange(result.Errors);

            if (result.EstimatedPromptTokens.HasValue)
            {
                estimatedTokens = result.EstimatedPromptTokens;
            }

            if (result.TokenBudget.HasValue)
            {
                tokenBudget = result.TokenBudget;
            }
        }

        var isValid = allErrors.All(error => error.Severity != ValidationSeverity.Error);
        return new PreAiValidationResult(isValid, allErrors, estimatedTokens, tokenBudget);
    }

    /// <summary>
    /// Checks if any validators are registered for a stage.
    /// </summary>
    public bool HasValidators(string stageName)
        => _validators.ContainsKey(stageName) && _validators[stageName].Count > 0;
}
