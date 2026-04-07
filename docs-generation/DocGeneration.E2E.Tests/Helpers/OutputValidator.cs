// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DocGeneration.E2E.Tests.Helpers;

/// <summary>
/// Validates the directory structure and file counts of pipeline output.
/// </summary>
public static class OutputValidator
{
    /// <summary>
    /// Returns all .md files in the specified subdirectory of the output path.
    /// </summary>
    public static string[] GetMarkdownFiles(string outputPath, string subdirectory)
    {
        var dir = Path.Combine(outputPath, subdirectory);
        if (!Directory.Exists(dir))
            return Array.Empty<string>();

        return Directory.GetFiles(dir, "*.md");
    }

    /// <summary>
    /// Returns all files matching a pattern in the specified subdirectory.
    /// </summary>
    public static string[] GetFiles(string outputPath, string subdirectory, string pattern = "*")
    {
        var dir = Path.Combine(outputPath, subdirectory);
        if (!Directory.Exists(dir))
            return Array.Empty<string>();

        return Directory.GetFiles(dir, pattern);
    }

    /// <summary>
    /// Extracts the tool base name from a generated filename by removing the suffix.
    /// Example: "azure-advisor-get-annotations.md" with suffix "-annotations" → "azure-advisor-get"
    /// </summary>
    public static string ExtractBaseName(string filePath, string suffix)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^suffix.Length]
            : fileName;
    }

    /// <summary>
    /// Gets the set of tool base names from a directory, stripping the given suffix.
    /// </summary>
    public static HashSet<string> GetToolBaseNames(string outputPath, string subdirectory, string suffix)
    {
        var files = GetMarkdownFiles(outputPath, subdirectory);
        return files
            .Select(f => ExtractBaseName(f, suffix))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
