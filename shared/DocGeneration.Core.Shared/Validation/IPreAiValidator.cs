// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Validation;

/// <summary>
/// Non-generic base interface for a seam validator that gates an AI stage.
/// Used for DI container resolution and pre-AI gate registry storage.
/// </summary>
public interface IPreAiValidator
{
    /// <summary>
    /// Gets the context type this seam validator expects.
    /// </summary>
    Type ContextType { get; }

    /// <summary>
    /// Validates a context object at the pre-AI gate.
    /// </summary>
    Task<PreAiValidationResult> ValidateAsync(object context, CancellationToken cancellationToken);
}

/// <summary>
/// Typed seam validator for compile-time safety. Implementations run at the pre-AI gate —
/// after the reducer or builder produces <typeparamref name="TContext"/> but before the LLM call
/// is dispatched. Returning a failing <see cref="PreAiValidationResult"/> causes the stage to be
/// skipped and <c>validationStatus: failed</c> to be written to the step envelope.
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
