// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Shared.Validation;

public sealed class ValidatorContextMismatchException : InvalidOperationException
{
    public string StageName { get; }

    public Type DeclaredType { get; }

    public Type AttemptedType { get; }

    public ValidatorContextMismatchException(string stageName, Type declaredType, Type attemptedType)
        : base($"Stage '{stageName}' declares context type '{declaredType.Name}' but validator provides '{attemptedType.Name}'.")
    {
        StageName = stageName;
        DeclaredType = declaredType;
        AttemptedType = attemptedType;
    }
}
