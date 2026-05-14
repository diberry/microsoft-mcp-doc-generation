// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace DocGeneration.Steps.ToolFamilyCleanup.Services;

/// <summary>
/// Validates that articles do not contain H3 headings except for tab markers.
/// Tab markers (#### [Label](#tab/tab-id)) are exempt as they're Learn tabbed conceptual format.
/// </summary>
public static class H3HeadingValidator
{
    // Matches any H3 heading (###) that is NOT a tab marker
    // Tab markers have format: #### [Label](#tab/tab-id)
    private static readonly Regex NonTabH3Regex = new(
        @"^###\s+(?!\[.*?\]\(#tab\/.*?\))",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public record ValidationResult(
        bool IsValid,
        IReadOnlyList<string> Errors);

    /// <summary>
    /// Validates that the markdown content contains no H3 headings except tab markers.
    /// </summary>
    public static ValidationResult Validate(string markdown)
    {
        var errors = new List<string>();
        
        if (string.IsNullOrEmpty(markdown))
        {
            return new ValidationResult(true, errors);
        }

        var lines = markdown.Split('\n');
        int lineNum = 0;

        foreach (var rawLine in lines)
        {
            lineNum++;
            var line = rawLine.TrimEnd('\r');

            // Check if this is an H3 heading
            if (line.TrimStart().StartsWith("### "))
            {
                var trimmedLine = line.Trim();
                
                // Allow tab markers: #### [Label](#tab/tab-id)
                if (IsTabMarker(trimmedLine))
                {
                    continue;
                }

                // This is a non-tab H3 heading - report error
                errors.Add($"Line {lineNum}: Found H3 heading (not allowed): {trimmedLine}");
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Checks if a line is a valid tab marker.
    /// Tab markers have format: #### [Label](#tab/tab-id)
    /// </summary>
    private static bool IsTabMarker(string line)
    {
        // Common tab markers in use:
        // #### [MCP Server](#tab/mcp-server)
        // #### [Azure MCP CLI](#tab/azure-mcp-cli)
        
        return line.StartsWith("#### [") && line.Contains("](#tab/");
    }
}
