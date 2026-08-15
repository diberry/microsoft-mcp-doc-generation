// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Coverage result for a single parameter.
/// </summary>
public sealed record SingleParameterCoverage(
    string CanonicalName,
    CoverageVerdict Verdict,
    string? MatchEvidence,
    int? MatchedPromptIndex);
