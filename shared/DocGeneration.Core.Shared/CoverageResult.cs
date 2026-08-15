// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Coverage result for an entire manifest's parameters.
/// </summary>
public sealed record CoverageResult(
    IReadOnlyList<SingleParameterCoverage> ParameterResults,
    bool AllRequiredCovered);
