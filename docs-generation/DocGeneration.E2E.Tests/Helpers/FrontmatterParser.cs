// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DocGeneration.E2E.Tests.Helpers;

/// <summary>
/// Parses YAML frontmatter from generated markdown files.
/// Extracts the key-value pairs between the opening and closing "---" markers.
/// </summary>
public static class FrontmatterParser
{
    /// <summary>
    /// Attempts to parse YAML frontmatter from markdown content.
    /// Returns null if no valid frontmatter block is found.
    /// </summary>
    public static Dictionary<string, string>? Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var lines = content.Split('\n');
        if (lines.Length < 2)
            return null;

        // First line must be "---"
        if (lines[0].Trim() != "---")
            return null;

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');

            // Closing delimiter
            if (line.Trim() == "---")
                return fields;

            // Skip YAML comments
            if (line.TrimStart().StartsWith('#'))
                continue;

            // Skip multi-line continuation (indented lines)
            if (line.StartsWith("  ") || line.StartsWith("\t"))
                continue;

            // Parse key: value
            var colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                var key = line[..colonIndex].Trim();
                var value = line[(colonIndex + 1)..].Trim();
                fields[key] = value;
            }
        }

        // No closing "---" found
        return null;
    }

    /// <summary>
    /// Reads a file and parses its frontmatter. Returns null if file doesn't exist or has no frontmatter.
    /// </summary>
    public static Dictionary<string, string>? ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var content = File.ReadAllText(filePath);
        return Parse(content);
    }

    /// <summary>
    /// Checks whether the content starts with a valid frontmatter block (--- ... ---).
    /// </summary>
    public static bool HasFrontmatter(string content)
    {
        return Parse(content) is not null;
    }
}
