// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Identifies and filters global/infrastructure CLI switches that should be
/// excluded from user-facing parameter tables and example commands.
/// </summary>
public static class GlobalSwitchFilter
{
    /// <summary>
    /// Returns true if the switch is a well-known global infrastructure option.
    /// </summary>
    public static bool IsGlobalSwitch(string switchName) =>
        switchName is "--subscription" or "--tenant" or "--tenant-id"
            or "--auth-method" or "--retry-delay" or "--retry-max-delay"
            or "--retry-max-retries" or "--retry-mode" or "--retry-network-timeout"
            or "--learn";

    /// <summary>
    /// Filters a list of switches, removing global infrastructure switches.
    /// </summary>
    public static IReadOnlyList<CliSwitch> FilterOutGlobal(IReadOnlyList<CliSwitch> switches) =>
        switches.Where(s => !IsGlobalSwitch(s.Name)).ToList();
}
