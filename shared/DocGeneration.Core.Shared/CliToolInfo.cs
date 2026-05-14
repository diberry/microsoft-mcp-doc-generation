// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// CLI data for a single azmcp tool/command.
/// Command is stored WITHOUT the "azmcp" prefix (e.g. "storage account list").
/// </summary>
public record CliToolInfo(
    string Command,
    string Description,
    IReadOnlyList<CliSwitch> Switches,
    bool IsDestructive = false,
    bool IsReadOnly = false,
    bool? EnrichmentMatched = null);
