// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Strips trailing pipe characters from annotation value lines.
/// The AI in Step 3 sometimes adds a trailing | to annotation lines
/// (e.g., "Destructive: ❌ | ... | Local Required: ❌ |").
/// Fixes: #281
/// </summary>
public static class AnnotationTrailingPipeFixer
{
    /// <summary>
    /// Removes trailing pipe characters from annotation value lines.
    /// Annotation lines are identified by starting with "Destructive:".
    /// Idempotent — lines without trailing pipes pass through unchanged.
    /// Does not affect markdown table rows (which start with |).
    /// </summary>
    public static string Fix(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";

        var lines = markdown.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("Destructive:") && trimmed.TrimEnd().EndsWith("|"))
            {
                lines[i] = lines[i].TrimEnd().TrimEnd('|').TrimEnd();
            }
        }
        return string.Join('\n', lines);
    }
}
