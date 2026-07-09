// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Converts annotation value lines from the old inline emoji-pair format to the
/// required 3-row markdown table format.
///
/// OLD (no longer accepted):
///   Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ✅
///
/// NEW (required):
///   | Destructive | Idempotent | Open World | Read Only | Secret | Local Required |
///   |:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|
///   | ❌ | ✅ | ❌ | ✅ | ❌ | ✅ |
///
/// Idempotent — already-table input passes through unchanged.
/// Field order is fixed: Destructive, Idempotent, Open World, Read Only, Secret, Local Required.
/// Fixes: annotation table format requirement per PR MicrosoftDocs/azure-dev-docs-pr#9391.
/// </summary>
public static class AnnotationTableFixer
{
    internal const string AnnotationLinkLine =
        "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):";

    internal const string HeaderRow =
        "| Destructive | Idempotent | Open World | Read Only | Secret | Local Required |";

    internal const string SeparatorRow =
        "|:-----------:|:----------:|:----------:|:---------:|:------:|:--------------:|";

    private static readonly string[] FieldNames =
        ["Destructive", "Idempotent", "Open World", "Read Only", "Secret", "Local Required"];

    // Matches the first line of an inline annotation: "Destructive: ✅" or "Destructive: ❌"
    private static readonly Regex InlineAnnotationRegex = new(
        @"^\s*Destructive\s*:\s*(✅|❌)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Matches the first line of a table-format annotation: "| Destructive |"
    private static readonly Regex TableHeaderRegex = new(
        @"^\s*\|\s*Destructive\s*\|",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Scans the markdown for annotation blocks under each
    /// "[Tool annotation hints](...)" link and converts any inline value lines
    /// to the 3-row table format. Idempotent — already-table blocks pass through unchanged.
    /// </summary>
    public static string Fix(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";

        var lines = markdown.Split('\n');
        var result = new List<string>(lines.Length + 8);
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];

            // Detect the annotation link line (trim \r for CRLF safety)
            if (line.TrimEnd('\r').TrimEnd() == AnnotationLinkLine)
            {
                result.Add(line);
                i++;

                // Collect any blank lines immediately after the link
                var blanks = new List<string>();
                while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
                {
                    blanks.Add(lines[i]);
                    i++;
                }

                if (i < lines.Length)
                {
                    var nextLine = lines[i].TrimEnd('\r');

                    if (InlineAnnotationRegex.IsMatch(nextLine))
                    {
                        // Convert inline → table.
                        // Emit exactly one blank line (AnnotationSpaceFixer will enforce it anyway).
                        result.Add("");
                        foreach (var row in ConvertInlineToTable(nextLine.Trim()))
                        {
                            result.Add(row);
                        }
                        i++; // consume the inline line
                    }
                    else if (TableHeaderRegex.IsMatch(nextLine))
                    {
                        // Already a table — restore blanks and let the outer loop process normally.
                        result.AddRange(blanks);
                        // Do NOT advance i — outer loop will add the table lines.
                    }
                    else
                    {
                        // Unknown content after link — restore blanks unchanged.
                        result.AddRange(blanks);
                    }
                }
                else
                {
                    result.AddRange(blanks);
                }

                continue;
            }

            result.Add(line);
            i++;
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// Converts an inline annotation line to the 3-row markdown table.
    /// Returns exactly 3 strings: header row, separator row, values row.
    /// </summary>
    internal static string[] ConvertInlineToTable(string inlineLine)
    {
        var values = ParseInlineAnnotation(inlineLine);
        var cells = FieldNames.Select(f => values.TryGetValue(f, out var v) ? v : "❌");
        var valueRow = "| " + string.Join(" | ", cells) + " |";
        return [HeaderRow, SeparatorRow, valueRow];
    }

    /// <summary>
    /// Parses an inline annotation line into a field-name→emoji dictionary.
    /// Example input: "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ✅"
    /// </summary>
    private static Dictionary<string, string> ParseInlineAnnotation(string line)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = line.Split('|');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            var colonIdx = trimmed.IndexOf(':');
            if (colonIdx < 0)
                continue;

            var key = trimmed[..colonIdx].Trim();
            var value = trimmed[(colonIdx + 1)..].Trim();
            if (!string.IsNullOrEmpty(key) && (value == "✅" || value == "❌"))
            {
                result[key] = value;
            }
        }
        return result;
    }
}
