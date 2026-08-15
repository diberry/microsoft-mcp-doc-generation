// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// A single parameter entry in the canonical parameter manifest.
/// </summary>
public sealed record CanonicalParameterEntry(
    string CanonicalName,
    string DisplayName,
    string[] DisplayAliases,
    string[] PlaceholderAliases,
    bool Required,
    string RequiredText,
    bool IsConditionalRequired,
    string Description);
