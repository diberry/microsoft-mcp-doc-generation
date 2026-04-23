// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Injects and preserves @mcpcli HTML comments after tool H2 headings.
/// Every tool H2 should be followed by: &lt;!-- @mcpcli {namespace} {resource} {action} --&gt;
/// Stub for TDD — implement to make McpCliCommentInjectionTests pass.
/// Fixes: #416 Item 3
/// </summary>
public static class McpCliCommentInjector
{
    // Regex to match H2 headings
    private static readonly Regex H2Pattern = new(@"^##\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    
    // Regex to match existing @mcpcli comments
    private static readonly Regex McpCliCommentPattern = new(@"<!--\s*@mcpcli\s+[^>]+-->", RegexOptions.Compiled);

    /// <summary>
    /// Injects an @mcpcli comment after the first H2 heading in content.
    /// If already present, does not duplicate.
    /// </summary>
    public static string Inject(string content, string @namespace, string resource, string action)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        var comment = FormatComment(@namespace, resource, action);

        // Check if comment already exists
        if (content.Contains(comment, StringComparison.Ordinal))
            return content;

        // Find first H2 heading
        var match = H2Pattern.Match(content);
        if (!match.Success)
            return content;

        // Insert comment after the H2 line
        var h2EndIndex = match.Index + match.Length;
        
        // Insert the comment with proper spacing
        var result = content.Insert(h2EndIndex, $"\n\n{comment}");
        return result;
    }

    /// <summary>
    /// Injects @mcpcli comments for multiple tools in a stitched markdown document.
    /// Each H2 tool heading gets a corresponding comment.
    /// </summary>
    public static string InjectAll(string markdown, (string @namespace, string resource, string action)[] tools)
    {
        if (string.IsNullOrEmpty(markdown) || tools == null || tools.Length == 0)
            return markdown;

        var result = markdown;
        var h2Matches = H2Pattern.Matches(markdown);
        
        // Process matches in reverse order to preserve string indices
        for (int i = Math.Min(h2Matches.Count, tools.Length) - 1; i >= 0; i--)
        {
            var match = h2Matches[i];
            var tool = tools[i];
            var comment = FormatComment(tool.@namespace, tool.resource, tool.action);
            
            // Check if this specific comment already exists near this H2
            var h2EndIndex = match.Index + match.Length;
            var searchStart = h2EndIndex;
            var searchEnd = Math.Min(h2EndIndex + 200, result.Length);
            var searchRegion = result.Substring(searchStart, searchEnd - searchStart);
            
            if (!searchRegion.Contains(comment, StringComparison.Ordinal))
            {
                result = result.Insert(h2EndIndex, $"\n\n{comment}");
            }
        }

        return result;
    }

    /// <summary>
    /// Formats a single @mcpcli comment string.
    /// </summary>
    public static string FormatComment(string @namespace, string resource, string action)
    {
        return $"<!-- @mcpcli {@namespace} {resource} {action} -->";
    }
}
