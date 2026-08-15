// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Raw input for building a canonical parameter manifest entry.
/// Used by the emitter's static BuildParameterManifest method.
/// </summary>
public sealed record RawParameterInput(
    string Name,
    string DisplayName,
    bool Required,
    string RequiredText,
    bool IsConditionalRequired,
    string Description);
