// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Represents a single CLI switch/option for an azmcp command.
/// </summary>
public record CliSwitch(
    string Name,
    string Description,
    string Type = "string",
    bool? IsRequired = null,
    string? Default = null,
    string? ShortAlias = null,
    string? ValuePlaceholder = null,
    IReadOnlyList<string>? AllowedValues = null);
