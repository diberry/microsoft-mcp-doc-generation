// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Validation;

/// <summary>
/// Non-generic base interface for DI and registry use.
/// </summary>
public interface IPreAiValidator
{
    /// <summary>
    /// Gets the context type this validator expects.
    /// </summary>
    Type ContextType { get; }

    /// <summary>
    /// Validates a context object.
    /// </summary>
    Task<PreAiValidationResult> ValidateAsync(object context, CancellationToken cancellationToken);
}

/// <summary>
/// Generic typed validator for compile-time safety.
/// </summary>
public interface IPreAiValidator<in TContext> : IPreAiValidator
{
    /// <summary>
    /// Validates a typed context object.
    /// </summary>
    Task<PreAiValidationResult> ValidateAsync(TContext context, CancellationToken cancellationToken);

    Type IPreAiValidator.ContextType => typeof(TContext);

    Task<PreAiValidationResult> IPreAiValidator.ValidateAsync(object context, CancellationToken cancellationToken)
        => ValidateAsync((TContext)context, cancellationToken);
}
