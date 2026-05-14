// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ToolFamilyCleanup.Models;

/// <summary>
/// Result of validating a collection of ToolContent objects for ordering prerequisites.
/// </summary>
public class ToolOrderingValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<ToolContent> InvalidTools { get; init; } = [];
}
