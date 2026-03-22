// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared;

/// <summary>
/// Validates merge group configuration in brand-to-server-mapping.json.
/// Per AD-011: multi-namespace articles use post-assembly merge with
/// config-driven grouping.
/// </summary>
public static class MergeGroupValidator
{
    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "primary", "secondary"
    };

    /// <summary>
    /// Validates all merge group configurations. Returns a list of error messages
    /// (empty = valid).
    /// </summary>
    public static List<string> Validate(IReadOnlyList<BrandMapping> mappings)
    {
        var errors = new List<string>();

        // Check individual entries for incomplete merge fields
        foreach (var m in mappings)
        {
            if (string.IsNullOrEmpty(m.MergeGroup))
                continue;

            if (!m.MergeOrder.HasValue)
                errors.Add($"'{m.McpServerName}': has mergeGroup '{m.MergeGroup}' but missing mergeOrder");

            if (string.IsNullOrEmpty(m.MergeRole))
                errors.Add($"'{m.McpServerName}': has mergeGroup '{m.MergeGroup}' but missing mergeRole");
            else if (!ValidRoles.Contains(m.MergeRole))
                errors.Add($"'{m.McpServerName}': invalid mergeRole '{m.MergeRole}' — must be 'primary' or 'secondary'");
        }

        // Group-level validation
        var groups = mappings
            .Where(m => !string.IsNullOrEmpty(m.MergeGroup))
            .GroupBy(m => m.MergeGroup!);

        foreach (var group in groups)
        {
            var primaries = group.Where(m =>
                string.Equals(m.MergeRole, "primary", StringComparison.OrdinalIgnoreCase)).ToList();

            if (primaries.Count == 0)
                errors.Add($"Merge group '{group.Key}': no primary namespace defined");
            else if (primaries.Count > 1)
                errors.Add($"Merge group '{group.Key}': multiple primary namespaces ({string.Join(", ", primaries.Select(p => p.McpServerName))})");

            // Check for duplicate mergeOrder values
            var orders = group
                .Where(m => m.MergeOrder.HasValue)
                .GroupBy(m => m.MergeOrder!.Value)
                .Where(g => g.Count() > 1);

            foreach (var dup in orders)
                errors.Add($"Merge group '{group.Key}': duplicate mergeOrder {dup.Key} ({string.Join(", ", dup.Select(d => d.McpServerName))})");
        }

        return errors;
    }
}
