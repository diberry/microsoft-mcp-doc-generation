// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Globalization;
using System.Linq;

namespace Shared;

/// <summary>
/// Builds display-friendly names for tools from command strings and original names
/// Handles various naming patterns and corner cases
/// </summary>
public static class DisplayNameBuilder
{
    /// <summary>
    /// Builds a display name from a command string (e.g., "applens resource diagnose" -> "Resource: Diagnose")
    /// </summary>
    /// <param name="command">The full command string (e.g., "applens resource diagnose")</param>
    /// <param name="originalName">The original tool name as fallback</param>
    /// <returns>A formatted display name suitable for H2 headers</returns>
    public static string BuildDisplayName(string command, string originalName)
    {
        // Fallback to original name if command is empty
        if (string.IsNullOrWhiteSpace(command))
            return FormatName(originalName);

        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
            return FormatName(originalName);

        // Skip the first part (area name, e.g., "applens", "storage", "sql")
        // and build from remaining parts based on command structure
        if (parts.Length > 1)
        {
            var displayParts = parts.Skip(1).ToArray();
            
            // Handle based on number of remaining parts
            if (displayParts.Length == 1)
            {
                // 2-word command: "applens diagnose" -> "Diagnose"
                return FormatName(displayParts[0]);
            }
            else if (displayParts.Length == 2)
            {
                // 3-word command: "applens resource diagnose" -> "Resource: Diagnose"
                var group = FormatName(displayParts[0]);
                var action = FormatName(displayParts[1]);
                return $"{group}: {action}";
            }
            else
            {
                // 4+ word command: "kubernetes service cluster get" -> "Service: Cluster Get"
                var group = FormatName(displayParts[0]);
                var remaining = string.Join(" ", displayParts.Skip(1).Select(p => FormatName(p)));
                return $"{group}: {remaining}";
            }
        }

        return FormatName(originalName);
    }

    /// <summary>
    /// Formats a name string with proper capitalization
    /// </summary>
    private static string FormatName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        // Split by hyphens, spaces, or underscores
        var words = System.Text.RegularExpressions.Regex.Split(name, @"[\s\-_]+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToArray();

        if (words.Length == 0)
            return "";

        // Capitalize each word
        var capitalized = words.Select(w => 
            CultureInfo.CurrentCulture.TextInfo.ToTitleCase(w.ToLowerInvariant())
        );

        return string.Join(" ", capitalized);
    }
}
