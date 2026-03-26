// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Strips HTML scaffolding comments from generated tool-family articles.
/// 
/// The tool composition step generates comments like
/// <!-- Required parameters: 3 - 'app', 'resource-group', 'database' -->
/// as internal scaffolding. These must not appear in published content.
/// 
/// Preserves <!-- @mcpcli {command} --> markers which are functional tool markers.
/// </summary>
public static class ScaffoldingCommentStripper
{
    // Matches any HTML comment EXCEPT those starting with <!-- @mcpcli
    // Uses Singleline so .*? spans newlines (for multiline comments)
    private static readonly Regex ScaffoldingCommentPattern = new(
        @"<!--(?!\s*@mcpcli\b).*?-->",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// Strips scaffolding HTML comments from the content.
    /// Preserves <!-- @mcpcli ... --> tool markers.
    /// </summary>
    public static string Strip(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        var result = ScaffoldingCommentPattern.Replace(content, "");

        // Clean up excessive blank lines left by removals (3+ → 2)
        result = Regex.Replace(result, @"\n{3,}", "\n\n");

        return result;
    }
}
