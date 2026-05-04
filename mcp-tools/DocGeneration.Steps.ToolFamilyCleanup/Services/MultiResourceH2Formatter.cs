// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Formats H2 tool headings for multi-resource pages using "Resource type: action" format.
/// Stub for TDD — implement to make H2MultiResourceFormatTests pass.
/// Fixes: #416 Item 1
/// </summary>
public static class MultiResourceH2Formatter
{
    // Known acronyms that should remain uppercase in display names.
    // "vm" and "vmss" are intentionally excluded — these are handled by heading overrides
    // (HeadingOverrideProvider) which map them to "Virtual machine" / "Virtual machine scale set".
    private static readonly HashSet<string> KnownAcronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "db", "sql", "api", "aks", "acr", "dns", "ip", "nsg",
        "vpn", "vnet", "hpc", "gpu", "cpu", "ssd", "hdd", "cdn", "waf", "rbac"
    };

    /// <summary>
    /// Formats a tool heading for a multi-resource page (fallback when no override exists).
    /// Format: "{ResourceDisplayName}: {FormattedAction}"
    /// Action is title-cased with hyphens replaced by spaces.
    /// </summary>
    public static string FormatToolHeading(string resourceType, string action)
    {
        var displayName = FormatResourceTypeDisplayName(resourceType);
        var formattedAction = FormatAction(action);
        return $"{displayName}: {formattedAction}";
    }

    /// <summary>
    /// Formats an action verb: replaces hyphens with spaces and title-cases the first word.
    /// Examples: "create" → "Create", "enable-crr" → "Enable crr", "soft-delete" → "Soft delete"
    /// </summary>
    internal static string FormatAction(string action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return action;

        var spaced = action.Replace('-', ' ');
        return char.ToUpper(spaced[0], CultureInfo.InvariantCulture) + spaced[1..];
    }

    /// <summary>
    /// Converts a resource type identifier to a human-readable display name.
    /// Examples: "disk" -> "Managed disk", "vmss" -> "VMSS", "vm" -> "VM"
    /// </summary>
    private static string FormatResourceTypeDisplayName(string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            return "General";

        var words = resourceType.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new string[words.Length];

        for (int i = 0; i < words.Length; i++)
        {
            if (KnownAcronyms.Contains(words[i]))
            {
                result[i] = words[i].ToUpperInvariant();
            }
            else if (i == 0)
            {
                // Title-case the first word, with special handling for "disk" -> "Managed disk"
                if (words[i].Equals("disk", StringComparison.OrdinalIgnoreCase))
                {
                    result[i] = "Managed disk";
                }
                else
                {
                    result[i] = char.ToUpper(words[i][0], CultureInfo.InvariantCulture) + words[i][1..];
                }
            }
            else
            {
                // Lowercase subsequent non-acronym words
                result[i] = words[i].ToLowerInvariant();
            }
        }

        return string.Join(" ", result);
    }
}

/// <summary>
/// Formats H2 tool headings for single-resource pages (preserves existing format).
/// Stub for TDD — implement to make H2MultiResourceFormatTests pass.
/// Fixes: #416 Item 1
/// </summary>
public static class SingleResourceH2Formatter
{
    /// <summary>
    /// Formats a tool heading for a single-resource page.
    /// Preserves existing "Action resource" format.
    /// </summary>
    public static string FormatToolHeading(string toolName)
    {
        // Single-resource pages keep the existing format unchanged
        return toolName;
    }
}
