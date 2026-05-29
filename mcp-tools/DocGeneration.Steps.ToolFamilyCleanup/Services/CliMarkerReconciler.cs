// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Deterministically reconciles @mcpcli markers in stitched markdown against
/// authoritative command values from tool files.
///
/// This is the "schema-constrained" safety net (approach #4): even if AI or
/// post-processors corrupt the CLI markers, this reconciler rewrites them to
/// match the canonical commands extracted by ToolReader at parse time.
///
/// Run this AFTER all other post-processors in the stitching pipeline.
/// Fixes: #638 (AI drift — compound word splitting in markers)
/// </summary>
public static class CliMarkerReconciler
{
    private static readonly Regex McpCliMarkerPattern = new(
        @"<!--\s*@mcpcli\s+([^>]+?)\s*-->",
        RegexOptions.Compiled);

    private static readonly Regex H2Pattern = new(
        @"^##\s+(.+)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Reconciles all @mcpcli markers in the markdown to match the authoritative
    /// command list. Matches markers to commands by position (Nth H2 section → Nth command).
    /// </summary>
    /// <param name="markdown">The stitched markdown content.</param>
    /// <param name="authoritativeCommands">
    /// Ordered list of canonical CLI commands (from ToolContent.Command).
    /// Null entries are skipped (tool had no @mcpcli annotation).
    /// </param>
    /// <returns>Markdown with all @mcpcli markers reconciled.</returns>
    public static string Reconcile(string markdown, IReadOnlyList<string?> authoritativeCommands)
    {
        if (string.IsNullOrEmpty(markdown) || authoritativeCommands.Count == 0)
            return markdown;

        // Find all existing @mcpcli markers in document order
        var markers = McpCliMarkerPattern.Matches(markdown);
        if (markers.Count == 0)
            return markdown;

        // Build replacement map: for each marker, determine the correct command
        // Strategy: positional matching (Nth marker → Nth authoritative command)
        var result = markdown;

        // Process in reverse order to preserve string indices during replacement
        for (int i = markers.Count - 1; i >= 0; i--)
        {
            var markerMatch = markers[i];
            var correspondingCommandIdx = i;

            if (correspondingCommandIdx >= authoritativeCommands.Count)
                continue;

            var correctCommand = authoritativeCommands[correspondingCommandIdx];
            if (string.IsNullOrEmpty(correctCommand))
                continue;

            var correctMarker = $"<!-- @mcpcli {correctCommand} -->";
            var currentMarker = markerMatch.Value;

            if (!string.Equals(currentMarker, correctMarker, StringComparison.Ordinal))
            {
                result = result.Remove(markerMatch.Index, markerMatch.Length)
                               .Insert(markerMatch.Index, correctMarker);
            }
        }

        return result;
    }

    /// <summary>
    /// Injects @mcpcli markers where missing and reconciles existing ones.
    /// For tools that have a Command but no marker in their section, injects one
    /// after the H2 heading. For existing markers, reconciles against authoritative values.
    /// </summary>
    /// <param name="markdown">The stitched markdown content.</param>
    /// <param name="authoritativeCommands">
    /// Ordered list of canonical CLI commands matching H2 section order.
    /// </param>
    /// <returns>Markdown with all markers present and correct.</returns>
    public static string ReconcileAndInject(string markdown, IReadOnlyList<string?> authoritativeCommands)
    {
        if (string.IsNullOrEmpty(markdown) || authoritativeCommands.Count == 0)
            return markdown;

        // First pass: reconcile existing markers
        var reconciled = Reconcile(markdown, authoritativeCommands);

        // Second pass: ensure every tool H2 has a marker
        var h2Matches = H2Pattern.Matches(reconciled);

        // Skip "## Related content" at the end
        var toolH2s = h2Matches
            .Where(m => !m.Groups[1].Value.Trim().Equals("Related content", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Process in reverse to preserve indices
        for (int i = Math.Min(toolH2s.Count, authoritativeCommands.Count) - 1; i >= 0; i--)
        {
            var h2Match = toolH2s[i];
            var command = authoritativeCommands[i];
            if (string.IsNullOrEmpty(command))
                continue;

            var expectedMarker = $"<!-- @mcpcli {command} -->";

            // Check if marker already exists after this H2
            var afterH2Start = h2Match.Index + h2Match.Length;
            var searchEnd = Math.Min(afterH2Start + 200, reconciled.Length);
            var searchRegion = reconciled[afterH2Start..searchEnd];

            if (!searchRegion.Contains("@mcpcli", StringComparison.Ordinal))
            {
                // No marker found — inject after the H2 line
                reconciled = reconciled.Insert(afterH2Start, $"\n\n{expectedMarker}");
            }
        }

        return reconciled;
    }
}
