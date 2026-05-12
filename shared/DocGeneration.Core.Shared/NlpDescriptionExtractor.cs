// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Shared;

/// <summary>
/// Extracts NLP tool descriptions from tools-raw markdown files.
/// NLP descriptions are the "source of truth" for tool behavior and should be used
/// as the basis for CLI descriptions.
/// </summary>
public static class NlpDescriptionExtractor
{
    // Matches the description paragraph(s) after the command comment and before the first placeholder or section heading.
    // Uses lazy match (+?) to capture up to the first boundary ({{, ##, or [Tool annotation).
    private static readonly Regex NlpDescriptionRegex = new(
        @"<!--\s*@mcpcli\s+.+?\s*-->\s*\n\s*\n([\s\S]+?)(?=\n\s*\n\{\{|(?:\n|\A)##|\[Tool annotation)",
        RegexOptions.Compiled);

    /// <summary>
    /// Extracts NLP descriptions from tools-raw files for all tools.
    /// Returns a dictionary mapping normalized command names to NLP descriptions.
    /// </summary>
    /// <param name="toolsRawDir">Directory containing tools-raw/*.md files</param>
    /// <param name="nameContext">File name context for deterministic filename resolution</param>
    /// <param name="commands">Commands to extract descriptions for</param>
    /// <returns>Dictionary mapping normalized command → NLP description</returns>
    public static async Task<Dictionary<string, string>> ExtractNlpDescriptionsAsync(
        string toolsRawDir,
        FileNameContext nameContext,
        IEnumerable<string> commands)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(toolsRawDir))
        {
            return result;
        }

        foreach (var command in commands)
        {
            var toolFileName = ToolFileNameBuilder.BuildToolFileName(command, nameContext);
            var toolFilePath = Path.Combine(toolsRawDir, toolFileName);

            if (!File.Exists(toolFilePath))
            {
                continue;
            }

            try
            {
                var content = await File.ReadAllTextAsync(toolFilePath);
                var nlpDesc = ExtractNlpDescription(content);
                if (!string.IsNullOrWhiteSpace(nlpDesc))
                {
                    var normalizedCommand = CliJsonMapper.NormalizeCommand(command);
                    result[normalizedCommand] = nlpDesc.Trim();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ Failed to extract NLP description for '{command}': {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts the NLP description from a tools-raw markdown file.
    /// Returns null if no description is found.
    /// </summary>
    internal static string? ExtractNlpDescription(string toolMarkdown)
    {
        // Strip frontmatter first
        var contentWithoutFrontmatter = FrontmatterUtility.StripFrontmatter(toolMarkdown);

        // Match the paragraph after the @mcpcli comment
        var match = NlpDescriptionRegex.Match(contentWithoutFrontmatter);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return null;
    }
}
