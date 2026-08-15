// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Verdict for a single parameter's coverage evaluation.
/// </summary>
public enum CoverageVerdict
{
    Concrete,
    AuthorizedPlaceholder,
    Missing,
    Ambiguous
}
