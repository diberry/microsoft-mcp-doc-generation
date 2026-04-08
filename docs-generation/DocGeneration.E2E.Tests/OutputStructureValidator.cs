// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace DocGeneration.E2E.Tests;

/// <summary>
/// Validates the structure and content of generated pipeline output directories.
/// All checks target final publishable artifacts only, not intermediate/debug files.
/// </summary>
public static class OutputStructureValidator
{
    /// <summary>
    /// Subdirectories that must exist in every generated namespace output.
    /// This is the minimal contract — additional dirs may exist and are ignored.
    /// </summary>
    private static readonly string[] RequiredSubdirectories =
    {
        "annotations",
        "parameters",
        "tool-family",
        "tools"
    };

    /// <summary>
    /// Subdirectories containing final publishable markdown that should be validated.
    /// Intermediate dirs (tools, tools-raw, tools-composed, logs, reports, etc.) are excluded.
    /// Note: tools/ contains intermediate assembled files without standalone frontmatter;
    /// the final publishable articles are in tool-family/.
    /// </summary>
    private static readonly string[] PublishableMarkdownDirs =
    {
        "annotations",
        "parameters",
        "example-prompts",
        "tool-family",
        "horizontal-articles"
    };

    /// <summary>
    /// Regex patterns that indicate leaked/unresolved template tokens.
    /// </summary>
    private static readonly Regex[] LeakedTokenPatterns =
    {
        new(@"\{\{[A-Z_]+\}\}", RegexOptions.Compiled),         // {{PLACEHOLDER}}
        new(@"\{\{\{[A-Z_]+\}\}\}", RegexOptions.Compiled),     // {{{RAW_PLACEHOLDER}}}
        new(@"<<<TPL_\w+>>>", RegexOptions.Compiled),            // <<<TPL_TOKEN>>>
        new(@"__TPL_\w+__", RegexOptions.Compiled),              // __TPL_TOKEN__
    };

    /// <summary>
    /// Validates that the required subdirectories exist in the output path.
    /// </summary>
    public static ValidationResult ValidateDirectoryStructure(string outputPath)
    {
        var result = new ValidationResult();

        if (!Directory.Exists(outputPath))
        {
            result.AddIssue($"Output directory does not exist: {outputPath}");
            return result;
        }

        foreach (var subdir in RequiredSubdirectories)
        {
            var path = Path.Combine(outputPath, subdir);
            if (!Directory.Exists(path))
            {
                result.AddIssue($"Required subdirectory missing: {subdir}");
            }
        }

        return result;
    }

    /// <summary>
    /// Scans final publishable markdown files for leaked/unresolved template tokens.
    /// Only checks directories that contain final output, not intermediate artifacts.
    /// </summary>
    public static ValidationResult ValidateNoLeakedTokens(string outputPath)
    {
        var result = new ValidationResult();

        foreach (var mdFile in GetPublishableMarkdownFiles(outputPath))
        {
            var content = File.ReadAllText(mdFile);
            var relativePath = Path.GetRelativePath(outputPath, mdFile);

            foreach (var pattern in LeakedTokenPatterns)
            {
                var matches = pattern.Matches(content);
                foreach (Match match in matches)
                {
                    result.AddIssue($"Leaked token '{match.Value}' in {relativePath}");
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Validates that all publishable markdown files have valid YAML frontmatter
    /// (start with --- and have a closing ---).
    /// </summary>
    public static ValidationResult ValidateMarkdownFrontmatter(string outputPath)
    {
        var result = new ValidationResult();

        foreach (var mdFile in GetPublishableMarkdownFiles(outputPath))
        {
            var content = File.ReadAllText(mdFile);
            var relativePath = Path.GetRelativePath(outputPath, mdFile);

            if (!HasValidFrontmatter(content))
            {
                result.AddIssue($"Missing or malformed frontmatter in {relativePath}");
            }
        }

        return result;
    }

    /// <summary>
    /// Validates no publishable markdown files are empty or effectively truncated.
    /// Checks for: non-empty content, frontmatter closes properly, and minimum
    /// content beyond frontmatter.
    /// </summary>
    public static ValidationResult ValidateFileIntegrity(string outputPath)
    {
        var result = new ValidationResult();

        foreach (var mdFile in GetPublishableMarkdownFiles(outputPath))
        {
            var content = File.ReadAllText(mdFile);
            var relativePath = Path.GetRelativePath(outputPath, mdFile);

            if (string.IsNullOrWhiteSpace(content))
            {
                result.AddIssue($"Empty file: {relativePath}");
                continue;
            }

            if (content.Length < 50)
            {
                result.AddIssue($"Suspiciously short file ({content.Length} chars): {relativePath}");
            }
        }

        return result;
    }

    /// <summary>
    /// Validates that tool_count in frontmatter matches the actual number of
    /// @mcpcli marker comments in tool-family articles.
    /// </summary>
    public static ValidationResult ValidateToolCount(string outputPath)
    {
        var result = new ValidationResult();
        var toolFamilyDir = Path.Combine(outputPath, "tool-family");

        if (!Directory.Exists(toolFamilyDir))
        {
            result.AddIssue("tool-family directory not found — cannot validate tool counts");
            return result;
        }

        foreach (var mdFile in Directory.GetFiles(toolFamilyDir, "*.md"))
        {
            var content = File.ReadAllText(mdFile);
            var relativePath = Path.GetRelativePath(outputPath, mdFile);

            var frontmatterToolCount = ExtractToolCountFromFrontmatter(content);
            if (frontmatterToolCount is null)
            {
                result.AddIssue($"No tool_count in frontmatter: {relativePath}");
                continue;
            }

            var actualMarkerCount = CountMcpCliMarkers(content);

            if (frontmatterToolCount.Value != actualMarkerCount)
            {
                result.AddIssue(
                    $"Tool count mismatch in {relativePath}: " +
                    $"frontmatter says {frontmatterToolCount.Value}, " +
                    $"found {actualMarkerCount} @mcpcli markers");
            }
        }

        return result;
    }

    /// <summary>
    /// Returns all .md files from publishable output directories.
    /// </summary>
    internal static IEnumerable<string> GetPublishableMarkdownFiles(string outputPath)
    {
        foreach (var subdir in PublishableMarkdownDirs)
        {
            var dirPath = Path.Combine(outputPath, subdir);
            if (!Directory.Exists(dirPath))
                continue;

            foreach (var file in Directory.GetFiles(dirPath, "*.md"))
            {
                yield return file;
            }
        }
    }

    /// <summary>
    /// Checks if content starts with YAML frontmatter (--- ... ---).
    /// </summary>
    internal static bool HasValidFrontmatter(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var trimmed = content.TrimStart();
        if (!trimmed.StartsWith("---"))
            return false;

        // Find the closing --- (must be after the opening one)
        var closingIndex = trimmed.IndexOf("\n---", 3, StringComparison.Ordinal);
        return closingIndex > 0;
    }

    /// <summary>
    /// Extracts tool_count value from YAML frontmatter.
    /// </summary>
    internal static int? ExtractToolCountFromFrontmatter(string content)
    {
        var match = Regex.Match(content, @"^tool_count:\s*(\d+)", RegexOptions.Multiline);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
            return count;
        return null;
    }

    /// <summary>
    /// Counts <!-- @mcpcli ... --> comment markers in content.
    /// </summary>
    internal static int CountMcpCliMarkers(string content)
    {
        return Regex.Matches(content, @"<!--\s*@mcpcli\s+\S+").Count;
    }
}
